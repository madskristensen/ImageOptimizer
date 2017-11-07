pngquant --speed 2 --skip-if-larger %1 --output %2
if not exist %2 copy %1 %2 /y
pingo -s6 -q %2