Wardrobe: Change textures on VirtaMate clothes
==============================================

Installation
------------

- Put `Wardrobe.cslist` and the `src` directory somewhere in `<vam>/Saves/Scripts`
- Attach the `Wardrobe.cslist` to Person atoms using the `Plugins` tab.
- Make some outfits (or download from the excellent outfit library
  maintained by Vince at https://mega.nz/#F!2eQUhAgI!E9R1yV1NY-qHEIuWxNwLkA)
- Apply outfits to particular clothing pieces using the plugin's UI.

## Making an outfit

An outfit is a directory inside `<vam>/Textures/Wardrobe/ClothingItem`
with textures for all, or part of the clothing item.

Every outfit can have a `default` texture set, and override specific
parts (such as `Skirt-1` or `TopFront-1`) with different textures.

Any outfit named `PSD` will be ignored, this is a good place for
creators to store template files.

## Naming textures

Texture names are three parts: `<part><type>.<ext>`

- `part` is the part of the clothing to be retextured, or `default`
- `type` is the type of material to which to apply the texture (optional)
- `ext` can be any unity-supported image type (png, jpg, tif, etc)

The `part` for each clothing item can differ, select the clothes in
the plugin to see a list of them.

The types are based on the way the texture should be used

- `D`: Diffuse (color)
- `A`: Alpha (transparency)
- `N`: Normal
- `S`: Specular
- `G`: Gloss
- none: If the texture type is omitted, it will be both diffuse and
        alpha (using the alpha channel of the texture image)

Obtaining geometry with UV maps
-------------------------------

Once a clothing item is selected, the option to export OBJs with UV
maps intact will be made available. These will be placed at the top
level of the `<vam>` install directory.

Gotchas, bugs, and future
-------

- Not checking to make sure texture to be loaded is actually an image.

Credits
-------

- MeshedVR for creating VirtaMate
- chokaphi for building the code for the first texture replacement
- VamSander for creating OBJ export functionality
- Vince for building a massive texture collection and making it open.
