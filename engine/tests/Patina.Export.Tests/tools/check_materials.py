"""Reads Patina's USD and MaterialX material files in their own libraries, and says what they find wrong.

Usage: python -I check_materials.py <file>...
       python -I check_materials.py --normals <file.usda>...

Each .usda is opened by Pixar's USD (usd-core) and each .mtlx by the MaterialX library, the readers Blender, Houdini,
Omniverse, Maya and Unreal use. A file is held to what they need to show it: it opens, its schema's own validation
passes, every shader is a known one, every input it wires comes from a picture that is there, and each picture is read
in the colour space its numbers are in. Prints one JSON object, {"<file>": ["<problem>", ...]}, and exits 0 either way:
the test that runs it reads the problems.

With --normals it prints, for each USD file, how USD reads the picture behind each UsdPreviewSurface normal:
{"<file>": [{"file": "<picture>", "output": "rgb", "scale": [4], "bias": [4]}]}. UsdUVTexture's result is each texel
times scale plus bias, so the test that runs it works out the normal a renderer is handed from the picture itself.
"""

import json
import os
import sys

COLOUR_INPUTS = {"diffuseColor", "emissiveColor", "specularColor", "base_color", "emission_color", "specular_color"}


def space_problem(name, output, file, space):
    """What is wrong with the space a UsdUVTexture reads a picture in, for the input it feeds, or None.

    sRGB decodes only a picture's colour lanes; its alpha stays linear, so a lane read from alpha may come from an sRGB
    picture. A float picture holds linear numbers, so it is raw. Data read from a colour lane must be raw. A colour may
    be either, since an 8-bit picture can hold sRGB numbers or a 16-bit one linear numbers.
    """
    floating = file.lower().endswith((".exr", ".hdr"))
    if floating and space != "raw":
        return f"reads {file} as {space}, where a float picture holds linear numbers"
    if name not in COLOUR_INPUTS and output != "a" and space != "raw":
        return f"reads data from {file} as {space}, where it is raw"
    if space not in ("raw", "sRGB"):
        return f"leaves the space of {file} to the reader ({space})"
    return None


# A normal whose green runs down is read through a texture with scale (2, -2, 2, 1) and bias (-1, 1, -1, 0)
# (UsdMaterialFile), the form the USD working group's NormalsTextureBiasAndScale asset gives a reversed-Y normal and
# that usdview and Blender's importer read. USD's validator warns about any 8-bit normal whose scale is not (2, 2, 2),
# since "some authoring tools may rely on this to scale an effect of normal perturbations" (its own comment); that
# warning, for exactly this scale, is the one accepted (#304). MaterialToolTests checks the normal USD reads.
ACCEPTED = (
    "NormalMapTextureValidator.NonCompliantScaleValues",
    "non-standard inputs:scale value of (2, -2, 2, 1)",
)


def check_usd(path):
    from pxr import Sdf, Usd, UsdShade, UsdValidation

    problems = []
    try:
        stage = Usd.Stage.Open(path)
    except Exception as e:  # noqa: BLE001 - USD raises its own Tf errors
        return [f"USD cannot open it: {e}"]

    # Every validator USD ships but the ones for .usdz packages, which a loose layer is not.
    registry = UsdValidation.ValidationRegistry()
    validators = [
        registry.GetOrLoadValidatorByName(metadata.name)
        for metadata in registry.GetAllValidatorMetadata()
        if "UsdzValidators" not in metadata.GetKeywords()
    ]
    for error in UsdValidation.ValidationContext(validators).Validate(stage):
        said = error.GetErrorAsString()
        if str(error.GetType()).rsplit(".", 1)[-1] in ("Error", "Warn") and not all(a in said for a in ACCEPTED):
            problems.append(f"validation: {said}")

    materials = [UsdShade.Material(prim) for prim in stage.Traverse() if prim.IsA(UsdShade.Material)]
    if not materials:
        problems.append("it has no material")
    for material in materials:
        name = material.GetPath()
        surface = material.ComputeSurfaceSource()[0]
        if not surface:
            problems.append(f"{name}: its surface output is not connected")
            continue
        if surface.GetShaderId() != "UsdPreviewSurface":
            problems.append(f"{name}: its surface is {surface.GetShaderId()}, not UsdPreviewSurface")
        for shader_input in surface.GetInputs():
            if not shader_input.HasConnectedSource():
                continue
            source, output, _ = shader_input.GetConnectedSource()
            texture = UsdShade.Shader(source.GetPrim())
            where = f"{name}.{shader_input.GetBaseName()}"
            if texture.GetShaderId() != "UsdUVTexture":
                problems.append(f"{where}: comes from {texture.GetShaderId()}, not UsdUVTexture")
                continue
            if output not in ("rgb", "r", "g", "b", "a"):
                problems.append(f"{where}: reads output {output}, which UsdUVTexture does not have")
            asset = texture.GetInput("file").Get() if texture.GetInput("file") else None
            if not isinstance(asset, Sdf.AssetPath) or not asset.path:
                problems.append(f"{where}: its texture names no file")
                continue
            resolved = asset.resolvedPath or os.path.join(os.path.dirname(path), asset.path)
            if not os.path.isfile(resolved):
                problems.append(f"{where}: {asset.path} is not there")
            space_input = texture.GetInput("sourceColorSpace")
            space = space_input.Get() if space_input else "auto"
            problem = space_problem(shader_input.GetBaseName(), output, asset.path, space)
            if problem:
                problems.append(f"{where}: {problem}")
            st = texture.GetInput("st")
            if not st or not st.HasConnectedSource():
                problems.append(f"{where}: its texture reads no texture coordinates")
    return problems


def check_materialx(path):
    import MaterialX as mx

    problems = []
    document = mx.createDocument()
    try:
        mx.readFromXmlFile(document, path)
    except Exception as e:  # noqa: BLE001 - MaterialX raises its own exceptions
        return [f"MaterialX cannot read it: {e}"]

    library = mx.createDocument()
    mx.loadLibraries(mx.getDefaultDataLibraryFolders(), mx.getDefaultDataSearchPath(), library)
    document.setDataLibrary(library)
    valid, message = document.validate()
    if not valid:
        problems += [f"validate: {line}" for line in message.strip().splitlines()]

    materials = [node for node in document.getMaterialNodes()]
    if not materials:
        problems.append("it has no material")
    folder = os.path.dirname(path)
    for node in document.traverseTree():
        if not isinstance(node, mx.Node):
            continue
        if node.getNodeDef() is None:
            problems.append(f"{node.getNamePath()}: no definition of {node.getCategory()} in the standard libraries")
        if node.getCategory() in ("image", "tiledimage", "gltf_image"):
            file_input = node.getInput("file")
            if file_input is None or not file_input.getValueString():
                problems.append(f"{node.getNamePath()}: names no file")
                continue
            name = file_input.getResolvedValueString()
            if not os.path.isfile(os.path.join(folder, name)):
                problems.append(f"{node.getNamePath()}: {name} is not there")
            space = file_input.getActiveColorSpace()
            colour = node.getType() in ("color3", "color4")
            wanted = "srgb_texture" if colour else ""
            if colour and space not in ("srgb_texture", "lin_rec709"):
                problems.append(f"{node.getNamePath()}: a color picture in {space or 'no colour space'}")
            if not colour and space not in ("", "lin_rec709", "raw"):
                problems.append(f"{node.getNamePath()}: data read as {space}, where {wanted or 'raw'} is wanted")
    return problems


def normals_of(path):
    """How USD reads each picture that feeds a UsdPreviewSurface normal: its file, output, scale and bias."""
    from pxr import Usd, UsdShade

    stage = Usd.Stage.Open(path)
    found = []
    for prim in stage.Traverse():
        shader = UsdShade.Shader(prim)
        if not shader or shader.GetShaderId() != "UsdPreviewSurface":
            continue
        normal = shader.GetInput("normal")
        if not normal or not normal.HasConnectedSource():
            continue
        source, output, _ = normal.GetConnectedSource()
        texture = UsdShade.Shader(source.GetPrim())

        def value(name, default):
            attributes = UsdShade.Utils.GetValueProducingAttributes(texture.GetInput(name))
            return list(attributes[0].Get()) if attributes else default

        found.append({
            "file": texture.GetInput("file").Get().path,
            "output": output,
            "scale": value("scale", [1, 1, 1, 1]),
            "bias": value("bias", [0, 0, 0, 0]),
        })
    return found


def main(files):
    if files[:1] == ["--normals"]:
        print(json.dumps({path: normals_of(path) for path in files[1:]}))
        return
    found = {}
    for path in files:
        if path.endswith(".usda") or path.endswith(".usd"):
            found[path] = check_usd(path)
        elif path.endswith(".mtlx"):
            found[path] = check_materialx(path)
        else:
            found[path] = ["not a USD or MaterialX file"]
    print(json.dumps(found))


if __name__ == "__main__":
    main(sys.argv[1:])
