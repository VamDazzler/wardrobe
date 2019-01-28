Wardrobe: Change textures on VirtaMate clothes
==============================================

To use this plugin:

- Put Wardrobe.cs into <vam>/Saves/Scripts
- Put clothing textures into either scene or global texture directory
  for the particular piece of clothing
- Start a scene with a character having clothes on
- Attach Wardrobe to the character and open the plugin menu.
- Select the clothing piece to recieve the replacement and then the
  texture for replacement

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
