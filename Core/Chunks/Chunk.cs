using System.Collections.Generic;

namespace Core;

public class Chunk
{
    public uint Id { get; set; } //IDchunk

    public uint Size { get; set; } //ChunkSize

    public long Offset { get; set; } //Offset where is located

    public byte[] Data { get; set; } //DATA

    public bool HasPadding => Padding > 0; 

    public uint Padding { get; set; }

    public uint PrePadding { get; set; }

    public BasicResource Resource { get; set; } 

    public List<Chunk> SubChunks { get; set; }
}
