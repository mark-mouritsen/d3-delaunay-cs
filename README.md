# d3-delaunay-cs
https://github.com/d3/d3-delaunay ported to c#.
Using Poisson Disc sampling from http://theinstructionlimit.com/fast-uniform-poisson-disk-sampling-in-c.

No optimizations or changes yet. See the demo for usage.

There are minor discrepancies due to differences in rounding of numbers between js number and c# double. Points may be ordered differently or positioned slightly differently in many cases.
