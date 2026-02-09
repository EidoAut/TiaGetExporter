/*
 * -------------------------------------------------------------------------
 *  TiaGitExporter
 /*
 * -------------------------------------------------------------------------
 *  TiaGitExporter
 * -------------------------------------------------------------------------
 *  Copyright (c) 2026 Eido Automation
 *  Version: v0.1
 *  License: MIT License
 *
 *  Description:
 *  TiaGitExporter is a tool designed to export and import Siemens TIA Portal
 *  PLC artifacts (UDTs, Blocks, Tag Tables, etc.) into a Git-friendly folder
 *  structure using the TIA Portal Openness API.
 *
 *  The application enables version control, change tracking, and automated
 *  backup of PLC software projects by converting TIA objects into structured
 *  XML files suitable for source control systems.
 *
 *  Developed by: Eido Automation
 * -------------------------------------------------------------------------
 */

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TiaGitExporter.Core.Openness {
    /// <summary>
    /// Runs all TIA Openness calls on a dedicated STA thread.
    ///
    /// Why STA?
    /// - TIA Openness objects are not thread-safe and behave similarly to COM components.
    /// - Keeping all Openness access on a single STA thread prevents cross-thread exceptions.
    ///
    /// Design notes:
    /// - Work is queued as delegates and executed serially in FIFO order.
    /// - Exceptions thrown by work items are marshalled back to the caller via TaskCompletionSource.
    /// - Cancellation is cooperative: it can prevent queueing / stop awaiting, but it cannot abort an in-progress Openness call.
    /// </summary>
    public sealed class StaWorker : IDisposable {
        /// <summary>
        /// Queue of work items to execute on the STA thread.
        /// BlockingCollection provides thread-safe producer/consumer semantics.
        /// </summary>
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

        /// <summary>
        /// The dedicated background STA thread.
        /// </summary>
        private readonly Thread _thread;

        /// <summary>
        /// Cancellation token used to stop the consuming enumerable on shutdown.
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private bool _disposed;

        /// <summary>
        /// Creates and starts the STA worker thread.
        /// </summary>
        /// <param name="name">Thread name (useful for debugging).</param>
        public StaWorker(string name = "TIA Openness STA Worker") {
            _thread = new Thread(Run) {
                IsBackground = true,
                Name = name
            };

            // Openness expects STA access (COM-like behavior).
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        /// <summary>
        /// Main worker loop: executes queued actions until completion or cancellation.
        /// </summary>
        private void Run() {
            try {
                foreach (var action in _queue.GetConsumingEnumerable(_cts.Token))
                    action();
            } catch (OperationCanceledException) {
                // Normal shutdown (cts cancelled).
            }
        }

        /// <summary>
        /// Executes a function on the STA thread and returns its result.
        /// </summary>
        public Task<T> InvokeAsync<T>(Func<T> func) {
            if (func == null) throw new ArgumentNullException(nameof(func));
            EnsureNotDisposed();

            // RunContinuationsAsynchronously avoids running continuations inline on the STA thread.
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            _queue.Add(() => {
                try { tcs.SetResult(func()); } catch (Exception ex) { tcs.SetException(ex); }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Executes a function on the STA thread and returns its result, honoring cancellation.
        ///
        /// Note:
        /// - Cancellation cannot abort an Openness call already executing.
        /// - Cancellation can prevent queueing (if already requested) or allow the caller to stop waiting.
        /// </summary>
        public Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken) {
            if (func == null) throw new ArgumentNullException(nameof(func));
            EnsureNotDisposed();

            // If already cancelled, avoid queueing any work.
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            // If the caller cancels while we are waiting, mark the task as cancelled.
            // The queued work may still run later, but TrySet* prevents double-completion.
            var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            _queue.Add(() => {
                try {
                    // If cancelled before execution begins, do not run user code.
                    if (cancellationToken.IsCancellationRequested) {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    tcs.TrySetResult(func());
                } catch (Exception ex) {
                    tcs.TrySetException(ex);
                } finally {
                    // Ensure we release the cancellation registration.
                    reg.Dispose();
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Executes an action on the STA thread.
        /// </summary>
        public Task InvokeAsync(Action action) {
            if (action == null) throw new ArgumentNullException(nameof(action));

            // Reuse the generic path to keep queueing and exception behavior consistent.
            return InvokeAsync(() => {
                action();
                return true;
            });
        }

        /// <summary>
        /// Throws if this instance has been disposed.
        /// </summary>
        private void EnsureNotDisposed() {
            if (_disposed) throw new ObjectDisposedException(nameof(StaWorker));
        }

        /// <summary>
        /// Stops the worker and releases resources.
        /// </summary>
        public void Dispose() {
            if (_disposed) return;
            _disposed = true;

            // Stop accepting new work items.
            try { _queue.CompleteAdding(); } catch { /* ignore */ }

            // Cancel the consuming enumerable so the STA thread can exit.
            try { _cts.Cancel(); } catch { /* ignore */ }

            // Best-effort: wait briefly for the STA thread to finish.
            try {
                if (_thread.IsAlive)
                    _thread.Join(millisecondsTimeout: 5000);
            } catch { /* ignore */ }

            _cts.Dispose();
        }
    }
}