using BitMiracle.LibTiff.Classic;
using System.Runtime.InteropServices;

// Deer Isle, Maine - bounding box (16x16 km)
const double BOX_SOUTH = 44.153;
const double BOX_NORTH = 44.297;
const double BOX_WEST = -68.773;
const double BOX_EAST = -68.577;

// Output grid size (power of 2 for voxel chunks)
const int OUTPUT_SIZE = 1024; // 1024x1024 = 16km at ~16m per cell

var inputPath = args.Length > 0 ? args[0]
    : Path.Combine("..", "..", "TerrainData", "copernicus_n44_w069.tif");

var outputDir = args.Length > 1 ? args[1]
    : Path.Combine("..", "..", "LostSpawns", "wwwroot", "maps");

var mapName = "deer_isle";

Console.WriteLine($"Reading GeoTIFF: {Path.GetFullPath(inputPath)}");

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"File not found: {inputPath}");
    return 1;
}

// Read GeoTIFF
using var tiff = Tiff.Open(inputPath, "r");
if (tiff == null)
{
    Console.Error.WriteLine("Failed to open TIFF file");
    return 1;
}

int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
int sampleFormat = tiff.GetField(TiffTag.SAMPLEFORMAT)?[0].ToInt() ?? 1;

Console.WriteLine($"TIFF: {width}x{height}, {bitsPerSample} bits/sample, format={sampleFormat}");

// Read geo transform from ModelTiepointTag and ModelPixelScaleTag
double originX = 0, originY = 0, pixelSizeX = 0, pixelSizeY = 0;

var tiepoint = tiff.GetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG);
var pixelScale = tiff.GetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG);

if (tiepoint != null && pixelScale != null)
{
    var tpBytes = tiepoint[1].ToByteArray();
    var psBytes = pixelScale[1].ToByteArray();

    var tpDoubles = MemoryMarshal.Cast<byte, double>(tpBytes);
    var psDoubles = MemoryMarshal.Cast<byte, double>(psBytes);

    // Tiepoint: I, J, K, X, Y, Z (raster point -> geo point)
    double tpI = tpDoubles[0], tpJ = tpDoubles[1];
    originX = tpDoubles[3]; // longitude of pixel (tpI, tpJ)
    originY = tpDoubles[4]; // latitude of pixel (tpI, tpJ)

    pixelSizeX = psDoubles[0];  // degrees per pixel (longitude)
    pixelSizeY = psDoubles[1];  // degrees per pixel (latitude)

    // Adjust origin to top-left corner
    originX -= tpI * pixelSizeX;
    originY += tpJ * pixelSizeY;

    Console.WriteLine($"Geo origin: ({originX:F6}, {originY:F6})");
    Console.WriteLine($"Pixel size: ({pixelSizeX:F8}, {pixelSizeY:F8}) degrees");
}
else
{
    Console.Error.WriteLine("Missing geo tags. Cannot determine coordinates.");
    return 1;
}

// Calculate pixel bounds for our bounding box
int colStart = Math.Max(0, (int)((BOX_WEST - originX) / pixelSizeX));
int colEnd = Math.Min(width - 1, (int)((BOX_EAST - originX) / pixelSizeX));
int rowStart = Math.Max(0, (int)((originY - BOX_NORTH) / pixelSizeY));
int rowEnd = Math.Min(height - 1, (int)((originY - BOX_SOUTH) / pixelSizeY));

int subWidth = colEnd - colStart + 1;
int subHeight = rowEnd - rowStart + 1;

Console.WriteLine($"Subregion: rows [{rowStart}-{rowEnd}] ({subHeight}), cols [{colStart}-{colEnd}] ({subWidth})");

// Read elevation data - handle both tiled and scanline TIFFs
var elevations = new float[subHeight, subWidth];
float minElev = float.MaxValue, maxElev = float.MinValue;
int voidCount = 0;
int bytesPerSample = bitsPerSample / 8;

bool isTiled = tiff.IsTiled();
Console.WriteLine($"TIFF is {(isTiled ? "tiled" : "scanline-based")}");

if (isTiled)
{
    int tileWidth = tiff.GetField(TiffTag.TILEWIDTH)[0].ToInt();
    int tileHeight = tiff.GetField(TiffTag.TILELENGTH)[0].ToInt();
    int tileSize = tiff.TileSize();

    Console.WriteLine($"Tile size: {tileWidth}x{tileHeight}, tile bytes: {tileSize}");

    // Cache decoded tiles to avoid re-reading
    var tileCache = new Dictionary<(int, int), byte[]>();

    for (int row = rowStart; row <= rowEnd; row++)
    for (int col = colStart; col <= colEnd; col++)
    {
        int tileRow = (row / tileHeight) * tileHeight;
        int tileCol = (col / tileWidth) * tileWidth;
        var tileKey = (tileRow, tileCol);

        if (!tileCache.TryGetValue(tileKey, out var tileBuf))
        {
            tileBuf = new byte[tileSize];
            int tileIdx = tiff.ComputeTile(tileCol, tileRow, 0, 0);
            tiff.ReadEncodedTile(tileIdx, tileBuf, 0, tileSize);
            tileCache[tileKey] = tileBuf;
        }

        int inTileRow = row - tileRow;
        int inTileCol = col - tileCol;
        int pixelOffset = (inTileRow * tileWidth + inTileCol) * bytesPerSample;

        if (pixelOffset < 0 || pixelOffset + bytesPerSample > tileBuf.Length) continue;

        float elev;
        if (bitsPerSample == 32 && sampleFormat == 3)
            elev = BitConverter.ToSingle(tileBuf, pixelOffset);
        else if (bitsPerSample == 16 && sampleFormat == 2)
            elev = BitConverter.ToInt16(tileBuf, pixelOffset);
        else
            elev = BitConverter.ToSingle(tileBuf, pixelOffset);

        if (elev < -1000 || float.IsNaN(elev))
        {
            elev = -1;
            voidCount++;
        }

        elevations[row - rowStart, col - colStart] = elev;
        if (elev > -1000)
        {
            minElev = Math.Min(minElev, elev);
            maxElev = Math.Max(maxElev, elev);
        }
    }
    Console.WriteLine($"Tiles cached: {tileCache.Count}");
}
else
{
    int scanlineSize = tiff.ScanlineSize();
    var scanline = new byte[scanlineSize];

    for (int row = rowStart; row <= rowEnd; row++)
    {
        tiff.ReadScanline(scanline, row);
        for (int col = colStart; col <= colEnd; col++)
        {
            int pixelOffset = col * bytesPerSample;
            float elev;
            if (bitsPerSample == 32 && sampleFormat == 3)
                elev = BitConverter.ToSingle(scanline, pixelOffset);
            else if (bitsPerSample == 16 && sampleFormat == 2)
                elev = BitConverter.ToInt16(scanline, pixelOffset);
            else
                elev = BitConverter.ToSingle(scanline, pixelOffset);

            if (elev < -1000 || float.IsNaN(elev))
            {
                elev = -1;
                voidCount++;
            }

            elevations[row - rowStart, col - colStart] = elev;
            if (elev > -1000)
            {
                minElev = Math.Min(minElev, elev);
                maxElev = Math.Max(maxElev, elev);
            }
        }
    }
}

Console.WriteLine($"Elevation range: {minElev:F1}m to {maxElev:F1}m");
Console.WriteLine($"Void pixels: {voidCount}");

// Resample to OUTPUT_SIZE x OUTPUT_SIZE using bilinear interpolation
var resampled = new float[OUTPUT_SIZE, OUTPUT_SIZE];

for (int outY = 0; outY < OUTPUT_SIZE; outY++)
for (int outX = 0; outX < OUTPUT_SIZE; outX++)
{
    float srcY = (float)outY / OUTPUT_SIZE * (subHeight - 1);
    float srcX = (float)outX / OUTPUT_SIZE * (subWidth - 1);

    int y0 = Math.Min((int)srcY, subHeight - 2);
    int x0 = Math.Min((int)srcX, subWidth - 2);
    float fy = srcY - y0;
    float fx = srcX - x0;

    float v00 = elevations[y0, x0];
    float v10 = elevations[y0, x0 + 1];
    float v01 = elevations[y0 + 1, x0];
    float v11 = elevations[y0 + 1, x0 + 1];

    resampled[outY, outX] = v00 * (1 - fx) * (1 - fy)
                          + v10 * fx * (1 - fy)
                          + v01 * (1 - fx) * fy
                          + v11 * fx * fy;
}

Console.WriteLine($"Resampled to {OUTPUT_SIZE}x{OUTPUT_SIZE}");

// Convert to voxel heights (signed short - meters, sea level = 0)
var heightmap = new short[OUTPUT_SIZE * OUTPUT_SIZE];
int waterCells = 0;

for (int y = 0; y < OUTPUT_SIZE; y++)
for (int x = 0; x < OUTPUT_SIZE; x++)
{
    float elev = resampled[y, x];
    short h = (short)Math.Clamp(Math.Round(elev), short.MinValue, short.MaxValue);
    heightmap[y * OUTPUT_SIZE + x] = h;
    if (h <= 0) waterCells++;
}

Console.WriteLine($"Water cells: {waterCells} ({100.0 * waterCells / (OUTPUT_SIZE * OUTPUT_SIZE):F1}%)");

// Write binary heightmap
Directory.CreateDirectory(outputDir);
var outputPath = Path.Combine(outputDir, $"{mapName}.heightmap");
var bytes = MemoryMarshal.AsBytes(heightmap.AsSpan());
File.WriteAllBytes(outputPath, bytes.ToArray());

Console.WriteLine($"Written: {outputPath} ({bytes.Length:N0} bytes)");

// Write metadata JSON
var metaPath = Path.Combine(outputDir, $"{mapName}.json");
var meta = $$"""
{
    "name": "Deer Isle",
    "description": "Coastal Maine terrain inspired by Deer Isle, Hancock County",
    "gridSize": {{OUTPUT_SIZE}},
    "cellSizeMeters": {{16000.0 / OUTPUT_SIZE:F2}},
    "totalSizeKm": 16.0,
    "seaLevelMeters": 0,
    "minElevation": {{minElev:F1}},
    "maxElevation": {{maxElev:F1}},
    "waterPercent": {{100.0 * waterCells / (OUTPUT_SIZE * OUTPUT_SIZE):F1}},
    "source": "Copernicus DEM 30m (ESA)",
    "bounds": {
        "south": {{BOX_SOUTH}},
        "north": {{BOX_NORTH}},
        "west": {{BOX_WEST}},
        "east": {{BOX_EAST}}
    },
    "realLocation": "Deer Isle & Stonington, Hancock County, Maine, USA",
    "format": "raw Int16 little-endian, row-major, north-to-south"
}
""";
File.WriteAllText(metaPath, meta);
Console.WriteLine($"Written: {metaPath}");
Console.WriteLine("Done!");

return 0;
