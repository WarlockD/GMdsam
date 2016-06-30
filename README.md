# My Game Maker Studio Decompiler and Undertale resource thing 
 
The code in here is my attempt to analyze and decompile the game Undertale that is currently programmed in Game Maker Studio. So far pulling any and all of the Undertale resources works fine (Including fonts, all sprite resources and info, images, audio, etc) 
 
This also decompiles the byte code into pseudo code. It also attempts to replace raw numerical values with object instances, sprite index, function names, etc.  It trys to de-optimizes some of the bytecode so the if statements come out alot cleaner. 

Will this work with other Game Maker Studio games?  "Maybe" as long as it uses bytecode.  
Will this decompiler work with newer versions of Studio Maker or versions other than Undertale 1.0? A: Properly not. Currently it only works with GameMaker Studio 8.0 and a beta version of the bytecode

While it works there is no GUI and many hand coded  stuff inside, but all you technically need is the Undertale.EXE or the data.win file and it will work. 

Anyway to use:

    Underdecomp data.win [-png] [-mask] [-constOffsets] [-oneFile] [-search] [chunktype|search_term]

* -png to extract all the sprites and backgrounds into individual files
* -mask extracts the calculated mask that is stored in the sprites if it exists.
* -constOffsets will add a comment to all assignments that have a simple constant left value. The offset is to the data.win's start so you can use this to mod the data.win.
* -oneFile combines all the xml/json data into one mega file. EXPERIMENTAL
* chunktype can be blank for eveything or one of these:
objects, backgrounds, code, sprites, fonts, scripts, rooms, sounds, paths, eveything
* -search will find code/sprites/rooms/etc that have text containing in [search_term] and output it to the directory the exe is at

For example, if you want all the sprites and the pngs from them, type:

    Underdecomp data.win -png sprites

It will extract everything to the folder where the exe is at.
