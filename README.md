Given an unpackaged mod, this tool:

* Converts png to dds (must have texconv.exe from DirectX toolkit on PATH)
* Builds .loca from .xml (credit to lsutils from which I adapted the code)
* Builds .lsf from .lsx
* Constructs texture atlases based on .json descriptions
* Constructs mod variations based on .json descriptions
* Packages into .pak
* Optionally deploys .pak
* Creates .zip from .pak

**You need to make sure you have divine.exe (lslib) and texconv.exe (DirectX SDK, or wheverer) on your PATH.**. Invoke like: `BG3ModPackager.exe [path_to_input_folder] --deploy [path_to_deploy_folder], where --deploy is optional.`

See https://github.com/Liareth/BG3Mods for an example of mods that use it.

This is a bit of a code dump - it mostly exists to make my life easier - so don't expect much support. Ta ta!
