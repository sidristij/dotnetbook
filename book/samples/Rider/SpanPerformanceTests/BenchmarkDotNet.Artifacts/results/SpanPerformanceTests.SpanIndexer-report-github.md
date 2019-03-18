``` ini

BenchmarkDotNet=v0.11.0, OS=Windows 10.0.16299.611 (1709/FallCreatorsUpdate/Redstone3)
Intel Core i7-6700 CPU 3.40GHz (Max: 3.41GHz) (Skylake), 1 CPU, 8 logical and 4 physical cores
Frequency=3328124 Hz, Resolution=300.4696 ns, Timer=TSC
.NET Core SDK=2.2.100-preview1-009349
  [Host]        : .NET Core 2.2.0-preview-26820-02 (CoreCLR 4.6.26820.03, CoreFX 4.6.26820.02), 64bit RyuJIT
  .NET 4.7      : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3132.0
  .NET 4.7.2    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3132.0
  .NET Core 2.1 : .NET Core 2.1.3 (CoreCLR 4.6.26725.06, CoreFX 4.6.26725.05), 64bit RyuJIT
  .NET Core 2.2 : .NET Core 2.2.0-preview-26820-02 (CoreCLR 4.6.26820.03, CoreFX 4.6.26820.02), 64bit RyuJIT


```
|                      Method |           Job |     Toolchain |      Mean |     Error |    StdDev |    Median | Scaled | ScaledSD |
|---------------------------- |-------------- |-------------- |----------:|----------:|----------:|----------:|-------:|---------:|
|            ArrayIndexer_Get |      .NET 4.7 |   CsProjnet47 | 0.6067 ns | 0.0121 ns | 0.0149 ns | 0.6030 ns |   1.00 |     0.00 |
|            ArrayIndexer_Set |      .NET 4.7 |   CsProjnet47 | 0.5861 ns | 0.0139 ns | 0.0130 ns | 0.5826 ns |   0.97 |     0.03 |
|     ArraySegmentIndexer_Get |      .NET 4.7 |   CsProjnet47 | 4.1642 ns | 0.0896 ns | 0.1255 ns | 4.1172 ns |   6.87 |     0.26 |
|     ArraySegmentIndexer_Set |      .NET 4.7 |   CsProjnet47 | 3.7260 ns | 0.0716 ns | 0.0704 ns | 3.7229 ns |   6.15 |     0.18 |
|           StringIndexer_Get |      .NET 4.7 |   CsProjnet47 | 0.6216 ns | 0.0128 ns | 0.0249 ns | 0.6185 ns |   1.03 |     0.05 |
|        SpanArrayIndexer_Get |      .NET 4.7 |   CsProjnet47 | 1.0164 ns | 0.0206 ns | 0.0193 ns | 1.0113 ns |   1.68 |     0.05 |
| SpanArraySegmentIndexer_Get |      .NET 4.7 |   CsProjnet47 | 0.8996 ns | 0.0179 ns | 0.0167 ns | 0.9003 ns |   1.48 |     0.04 |
|       SpanStringIndexer_Get |      .NET 4.7 |   CsProjnet47 | 1.0267 ns | 0.0160 ns | 0.0202 ns | 1.0243 ns |   1.69 |     0.05 |
|        SpanArrayIndexer_Set |      .NET 4.7 |   CsProjnet47 | 1.0127 ns | 0.0200 ns | 0.0187 ns | 1.0094 ns |   1.67 |     0.05 |
| SpanArraySegmentIndexer_Set |      .NET 4.7 |   CsProjnet47 | 1.0722 ns | 0.0218 ns | 0.0491 ns | 1.0671 ns |   1.77 |     0.09 |
|                             |               |               |           |           |           |           |        |          |
|            ArrayIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 0.6072 ns | 0.0125 ns | 0.0149 ns | 0.6042 ns |   1.00 |     0.00 |
|            ArrayIndexer_Set |    .NET 4.7.2 |  CsProjnet472 | 0.5849 ns | 0.0161 ns | 0.0192 ns | 0.5811 ns |   0.96 |     0.04 |
|     ArraySegmentIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 4.1139 ns | 0.0823 ns | 0.1070 ns | 4.0840 ns |   6.78 |     0.24 |
|     ArraySegmentIndexer_Set |    .NET 4.7.2 |  CsProjnet472 | 3.7793 ns | 0.0747 ns | 0.1206 ns | 3.7473 ns |   6.23 |     0.24 |
|           StringIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 0.6090 ns | 0.0151 ns | 0.0141 ns | 0.6025 ns |   1.00 |     0.03 |
|        SpanArrayIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 1.0338 ns | 0.0204 ns | 0.0362 ns | 1.0238 ns |   1.70 |     0.07 |
| SpanArraySegmentIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 0.9446 ns | 0.0193 ns | 0.0514 ns | 0.9431 ns |   1.56 |     0.09 |
|       SpanStringIndexer_Get |    .NET 4.7.2 |  CsProjnet472 | 1.0588 ns | 0.0214 ns | 0.0521 ns | 1.0430 ns |   1.74 |     0.09 |
|        SpanArrayIndexer_Set |    .NET 4.7.2 |  CsProjnet472 | 1.0172 ns | 0.0206 ns | 0.0361 ns | 1.0033 ns |   1.68 |     0.07 |
| SpanArraySegmentIndexer_Set |    .NET 4.7.2 |  CsProjnet472 | 1.0565 ns | 0.0215 ns | 0.0528 ns | 1.0353 ns |   1.74 |     0.10 |
|                             |               |               |           |           |           |           |        |          |
|            ArrayIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 0.7173 ns | 0.0144 ns | 0.0260 ns | 0.7185 ns |   1.00 |     0.00 |
|            ArrayIndexer_Set | .NET Core 2.1 | .NET Core 2.1 | 0.5920 ns | 0.0122 ns | 0.0140 ns | 0.5942 ns |   0.83 |     0.04 |
|     ArraySegmentIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 3.7554 ns | 0.0753 ns | 0.1653 ns | 3.7177 ns |   5.24 |     0.30 |
|     ArraySegmentIndexer_Set | .NET Core 2.1 | .NET Core 2.1 | 3.7553 ns | 0.0733 ns | 0.1265 ns | 3.7574 ns |   5.24 |     0.26 |
|           StringIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 0.6926 ns | 0.0140 ns | 0.0177 ns | 0.6920 ns |   0.97 |     0.04 |
|        SpanArrayIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 0.6209 ns | 0.0127 ns | 0.0136 ns | 0.6228 ns |   0.87 |     0.04 |
| SpanArraySegmentIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 0.6320 ns | 0.0125 ns | 0.0139 ns | 0.6301 ns |   0.88 |     0.04 |
|       SpanStringIndexer_Get | .NET Core 2.1 | .NET Core 2.1 | 0.6363 ns | 0.0075 ns | 0.0070 ns | 0.6351 ns |   0.89 |     0.03 |
|        SpanArrayIndexer_Set | .NET Core 2.1 | .NET Core 2.1 | 0.7311 ns | 0.0150 ns | 0.0284 ns | 0.7311 ns |   1.02 |     0.05 |
| SpanArraySegmentIndexer_Set | .NET Core 2.1 | .NET Core 2.1 | 0.6564 ns | 0.0113 ns | 0.0105 ns | 0.6592 ns |   0.92 |     0.04 |
|                             |               |               |           |           |           |           |        |          |
|            ArrayIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 0.7160 ns | 0.0146 ns | 0.0353 ns | 0.7037 ns |   1.00 |     0.00 |
|            ArrayIndexer_Set | .NET Core 2.2 | .NET Core 2.2 | 0.6066 ns | 0.0118 ns | 0.0105 ns | 0.6033 ns |   0.85 |     0.04 |
|     ArraySegmentIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 3.5756 ns | 0.0898 ns | 0.0840 ns | 3.5657 ns |   5.01 |     0.26 |
|     ArraySegmentIndexer_Set | .NET Core 2.2 | .NET Core 2.2 | 3.5116 ns | 0.0401 ns | 0.0375 ns | 3.5259 ns |   4.92 |     0.24 |
|           StringIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 0.6785 ns | 0.0078 ns | 0.0073 ns | 0.6821 ns |   0.95 |     0.05 |
|        SpanArrayIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 0.6331 ns | 0.0095 ns | 0.0089 ns | 0.6344 ns |   0.89 |     0.04 |
| SpanArraySegmentIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 0.6433 ns | 0.0138 ns | 0.0234 ns | 0.6345 ns |   0.90 |     0.05 |
|       SpanStringIndexer_Get | .NET Core 2.2 | .NET Core 2.2 | 0.6227 ns | 0.0109 ns | 0.0102 ns | 0.6243 ns |   0.87 |     0.04 |
|        SpanArrayIndexer_Set | .NET Core 2.2 | .NET Core 2.2 | 0.6631 ns | 0.0128 ns | 0.0119 ns | 0.6658 ns |   0.93 |     0.05 |
| SpanArraySegmentIndexer_Set | .NET Core 2.2 | .NET Core 2.2 | 0.6225 ns | 0.0128 ns | 0.0162 ns | 0.6201 ns |   0.87 |     0.05 |
