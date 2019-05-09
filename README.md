# BigBitmap
Simple Customized Bitmap Image Format which Supports 32M * 32M Pixels in 24bpp using MemoryMappedFiles Tech

since im working in vs2010, there is no async/await functionality (maybe in future)

to create a bigbitmap u got 2 options :

1- call BigBitmap.CreateBitBigmap(filepath) to create the bbmp file

2- create a new BigBitmap Object from existing file by calling new BigBitmap(filepath)


since its a custom image format, there is no Graphic support (sry about that) but it Supports drawLine, drawRectangle and DrawCircle (maybe DrawEllipse in the future)

**known issues :

GetChunk(..) function DOES NOT WORK !

Trying to save(to bitmap format) or converting to bitmap with any dimensions more than 2 MemoryPage(~131000 pixel each) will result in a corrupted file/format


