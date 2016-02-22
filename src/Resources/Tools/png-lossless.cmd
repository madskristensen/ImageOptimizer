::optipng %1 -out %2 -o5 -i0
::pngout %1 %2 /s0 /y /kpHYs
::pngzopfli %1 10 %2
truepng /o4 %1 /out %2
zopflipng --ohh %2 %2.png
copy %2.png %2 /y
del %2.png