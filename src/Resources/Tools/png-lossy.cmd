pngquant --speed 1 --skip-if-larger %1 --output %2

::optipng %2 -out %2 -o3 -i0
::pngout %2 %2 /s0 /y /kpHYs
if not exist %2 copy %1 %2

truepng /o4 %2
pngout %2 %2 /s0 /y /kpHYs
zopflipng --ohh %2 %2.png

copy %2.png %2 /y
del %2.png


::advpng --recompress %2
::advdef -z -4 %2
