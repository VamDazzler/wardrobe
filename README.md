Wardrobe: Change textures on VirtaMate clothes
==============================================

Installation
------------

- Put Wardrobe.cs into `<vam>/Saves/Scripts`
- Attach the script to Person atoms.

### Replacing textures on clothes

- Put clothing textures into either scene or global texture directory
  for the particular piece of clothing
- Attach Wardrobe to the character and open the plugin menu.
- Select the clothing piece, skin, and material to be replaced
- Set the type of texture(s) to replace on the material
- Apply.

Where to put textures
---------------------

Textures can be placed in either globbaly or relative to the scene
file. The particular directory will depend on the clothing item.

Wardrobe will log a message with the proper subdirectory and file
names if it finds none, so if you don't know the subdirectory, just
ask Wardrobe.

e.g. A texture for the Heatwave Shirt can go into either:

    <vam>/Textures/HeatwaveShirt/HeatwaveShirt/Shirt-1.png

or

    <scene's dir>/Textures/HeatwaveShirt/HeatwaveShirt/Shirt-1.png

Flexible texture names
----------------------

Wardrobe only checks that a filename is prefixed with the material
name, so you can have several textures for the same piece of clothing
with different names or just different extensions.

e.g. You have an everyday texture and a special texture for the
Heatwave Shirt:

    <vam>/Textures/HeatwaveShirt/HeatwaveShirt/Shirt-1-everyday.jpg
    <vam>/Textures/HeatwaveShirt/HeatwaveShirt/Shirt-1-special.png

Obtaining geometry with UV maps
-------------------------------

Once a clothing item is selected, the option to export OBJs with UV
maps intact will be made available. These will be placed at the top
level of the `<vam>` install directory.

Gotchas, bugs, and future
-------

- Not checking to make sure texture to be loaded is actually an image.

- Definitely want more shader property names (specifically specular
  and normal)
- Maybe want to have the texture selection toggles be more mutually
  exclusive.
- Maybe want to have the texture selection toggles depend on whether
  the shader actually has the property in question.
- Mass load suffix-style?
