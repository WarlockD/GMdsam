# My Game Maker Studio Decompiler and Undertale resource thing 
 
The code in here is my attempt to analyze and decompile the game Undertale that is currently programmed in Game Maker Studio. So far pulling any and all of the Undertale resources works fine (Including fonts, all sprite resources and info, images, audio, etc) 
 
This also decompiles the byte code into pseudo code. It also attempts to replace raw numerical values with object instances, sprite index, function names, etc.  It trys to de-optimizes some of the bytecode so the if statements come out alot cleaner. 

Will this work with other Game Maker Studio games?  A: I don't know 
Will this decompiler work with newer versions of Studio Maker or versions other than Undertale 1.0? A: Properly not. 

While it works there is no GUI and many hand coded  stuff inside, but all you technically need is the Undertale.EXE or the data.win file and it will work. 

One bug that has been plaguing me is the Dup opcode.  This opcode can either duplicate one object on the top of the stack OR either two items on the stack or just one?  All I know is it screws up half the time when I detect it.  I need to find more data on this.  Also the branch codes rely on the stack being lined up on each branch and of course I have no branch following code.   
Good luck!
