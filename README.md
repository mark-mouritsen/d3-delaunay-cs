# d3-delaunay-cs
https://github.com/d3/d3-delaunay ported to c#.

No optimizations or changes yet. See the demo for usage.

There are minor discrepancies due to differences in rounding of numbers between js number and c# double. Points may be ordered differently or positioned slightly differently in many cases.

There may be some issue with the delaunay.neighbors(i) function as it seems to pick up weird neighbors at the edges. I haven't compared the two versions to see if they are behaving similarly or if it's a defect from the port.
