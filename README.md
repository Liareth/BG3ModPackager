Build, then invoke the .exe like: BG3ModPackager.exe [path_to_input_folder] [path_to_output_folder] --deploy [path_to_deploy_folder], where --deploy is optional.

Given an unpackaged mod, this code does:

* Converts png to dds (must have texconv.exe from DirectX toolkit on PATH)
* Constructs a texture atlas from any lsx which has "Atlas" in its name
* Builds .loca from .xml (credit to lsutils from which I adapted the code)
* Builds .lsf from .lsx
* Packages into .pak
* Optionally deploys .pak

READ BEFORE USING:

* You need to unhardcode the divine.exe path from the code.
* You need to unhardcode the DyeDyeDye reference in the code.

This is a bit of a code dump, so don't expect much support. Ta ta