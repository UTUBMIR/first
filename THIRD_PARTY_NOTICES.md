# Third-Party Notices

## SHA-256 implementation

The SHA-256 compression logic used by the GPU miner in `GpuMining/GpuMineKernel.cs`
and `GpuMining/GpuSha256.cs` is adapted from the public-domain SHA-256
implementation by **Brad Conte**

- Author: Brad Conte (brad@bradconte.com)
- Source: https://github.com/B-Con/crypto-algorithms/blob/master/sha256.c
- License: Public domain. The author requests acknowledgement if the code is
  used, but does not require it

This project contains a modified C#/ILGPU port of that implementation. The
original C source is not included, and this port is not endorsed by the original
author
