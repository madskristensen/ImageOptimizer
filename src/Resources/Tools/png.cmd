pngquant --speed 1 %1 --output %2
::optipng %2 -out %2 -o3 -i0
pngout %2 %2 /s1 /y /kpHYs
pngzopfli %2 15 %2