# TiaGitExporter

WinForms utility that uses **TIA Portal Openness V19** to export PLC "code" (UDTs, blocks, optional PLC tag tables) into a Git-friendly XML folder structure, and to import those XML artifacts back into an existing project.

## Requirements

- TIA Portal V19 installed on the machine.
- TIA Openness installed/enabled.
- .NET Framework 4.8.
- User must belong to the Siemens user group and run the application as Administrator.

## Add Openness references (Siemens.*)

Due to licensing, Siemens Openness DLLs are **not** included.

In Visual Studio, add references to **TiaGitExporter.Core** from your local TIA installation (typical path):

`C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19\`

Recommended minimum set:

- Siemens.Engineering.dll
- Siemens.Engineering.HW.dll
- Siemens.Engineering.SW.dll
- Siemens.Engineering.SW.Blocks.dll
- Siemens.Engineering.SW.Tags.dll

Set **Copy Local = False** for each Siemens.* reference.

## Optional: AssemblyResolve

The WinForms app tries to locate the Openness DLLs at runtime.

You can set the environment variable `TIA_OPENNESS_V19` to point to your `PublicAPI\V19` folder.

## Git quick start

```bash
git init
git add .
git commit -m "Initial commit"
# create repo on GitHub then:
git remote add origin https://github.com/<user>/<repo>.git
git branch -M main
git push -u origin main
```
