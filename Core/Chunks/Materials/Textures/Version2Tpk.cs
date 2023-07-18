﻿using System.Runtime.InteropServices;
using Core.Textures.Data;

namespace Core.Textures
{
    /// <summary>
    /// TPK support for NFS:C, NFS:PS, NFS:UC(?)
    /// </summary>
    public class Version2Tpk : TpkManager
    {
        private const uint InfoChunkId = 0x33310001;
        private const uint HashChunkId = 0x33310002;
        private const uint TexChunkId = 0x33310004;
        private const uint DDSChunkId = 0x33310005;
        private const uint TexDataChunkId = 0x33320002;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TextureStruct
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            private byte[] blank;

            public uint Hash;
            public uint ClassNameHash;
            public uint ImagePlacement;
            public uint PalettePlacement;
            public uint ImageSize;
            public uint PaletteSize;
            public uint BaseImageSize;
            public ushort Width;
            public ushort Height;
            public byte ShiftWidth;
            public byte ShiftHeight;
            public TextureCompressionType ImageCompressionType;
            public byte PaletteCompressionType;
            public ushort NumPaletteEntries;
            public byte NumMipMapLevels;
            public byte TilableUV;
            public byte BiasLevel;
            public byte RenderingOrder;
            public byte ScrollType;
            public byte pad;
            public byte ApplyAlphaSorting;
            public byte AlphaUsageType;
            public byte AlphaBlendType;
            public byte Flags;
            public byte MipmapBiasType;
            public byte pad2;
            public short ScrollTimeStep;
            public short ScrollSpeedS;
            public short ScrollSpeedT;
            public short OffsetS;
            public short OffsetT;
            public short ScaleS;
            public short ScaleT;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] pad3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DataOffsetStruct
        {
            public uint Hash;
            public uint Offset;
            public int LengthCompressed;
            public int Length;
            public uint Flags;
            private uint blank;
        }

        private TexturePack _texturePack;

        private uint _offset, _size;
        private bool _compressed = false;

        public override TexturePack ReadTexturePack(BinaryReader br, uint containerSize)
        {
            _texturePack = new TexturePack();
            _offset = (uint)(br.BaseStream.Position - 8);
            _size = containerSize + 8;

            ReadChunks(br, containerSize);

            return _texturePack;
        }

        public override void WriteTexturePack(ChunkStream cs, TexturePack texturePack)
        {
            throw new NotImplementedException();
        }

        protected override void ReadChunks(BinaryReader br, uint containerSize)
        {
            var endPos = br.BaseStream.Position + containerSize;

            while (br.BaseStream.Position < endPos)
            {
                var chunkId = br.ReadUInt32();
                var chunkSize = br.ReadUInt32();
                var chunkEndPos = br.BaseStream.Position + chunkSize;

                if ((chunkId & 0x80000000) == 0x80000000)
                {
                    ReadChunks(br, chunkSize);
                }
                else
                {
                    switch (chunkId)
                    {
                        case InfoChunkId:
                            {
                                _texturePack.Version = br.ReadUInt32();
                                _texturePack.Name = new string(br.ReadChars(28)).Trim('\0');
                                _texturePack.PipelinePath = new string(br.ReadChars(64)).Trim('\0');
                                _texturePack.Hash = br.ReadUInt32();

                                break;
                            }
                        case HashChunkId:
                            {
                                _texturePack.Textures = new List<Texture>((int)(chunkSize / 8));
                                break;
                            }
                        case 0x33310003:
                            {
                                _compressed = true;

                                while (br.BaseStream.Position < chunkEndPos)
                                {
                                    var tch = br.GetStruct<DataOffsetStruct>();
                                    var curPos = br.BaseStream.Position;
                                    br.BaseStream.Position = tch.Offset;

                                    // Load decompressed texture
                                    using (var ms = new MemoryStream())
                                    {
                                        Compression.DecompressCip(br.BaseStream, ms, tch.LengthCompressed);

                                        using (var dcr = new BinaryReader(ms))
                                        {
                                            // Seek to TextureInfo
                                            dcr.BaseStream.Seek(-0x94, SeekOrigin.End);
                                            var texture = ReadTexture(dcr);
                                            // Seek to D3DFormat
                                            dcr.BaseStream.Seek(-0xC, SeekOrigin.End);
                                            texture.Format = dcr.ReadUInt32();
                                            // Read texture data
                                            dcr.BaseStream.Seek(0, SeekOrigin.Begin);
                                            if (dcr.BaseStream.Read(texture.Data, 0, texture.Data.Length) !=
                                                texture.Data.Length)
                                            {
                                                throw new Exception(
                                                    $"Failed to read data for texture 0x{texture.TexHash:X8} ({texture.Name})");
                                            }
                                        }
                                    }

                                    br.BaseStream.Position = curPos;
                                }

                                break;
                            }
                        case TexChunkId:
                            {
                                for (var j = 0; j < _texturePack.Textures.Capacity; j++)
                                {
                                    ReadTexture(br);
                                }

                                break;
                            }
                        case TexDataChunkId:
                            {
                                if (!_compressed)
                                {
                                    br.BaseStream.Position += 0x78;

                                    var basePos = br.BaseStream.Position;

                                    foreach (var t in _texturePack.Textures)
                                    {
                                        br.BaseStream.Position = basePos + t.DataOffset;
                                        br.Read(t.Data, 0, t.Data.Length);
                                    }
                                }

                                break;
                            }
                        case DDSChunkId:
                            {
                                foreach (var t in _texturePack.Textures)
                                {
                                    br.BaseStream.Seek(0x0C, SeekOrigin.Current);
                                    t.Format = br.ReadUInt32();
                                    br.BaseStream.Seek(0x08, SeekOrigin.Current);
                                }

                                break;
                            }
                    }
                }

                br.BaseStream.Position = chunkEndPos;
            }
        }

        private Texture ReadTexture(BinaryReader br)
        {
            var texture = BinaryUtil.ReadStruct<TextureStruct>(br);
            var nameLength = br.ReadByte();

            var name = new string(br.ReadChars(nameLength)).TrimEnd('\0');

            _texturePack.Textures.Add(new Texture
            {
                Width = texture.Width,
                Height = texture.Height,
                Name = name,
                Data = new byte[texture.ImageSize],
                DataSize = texture.ImageSize,
                DataOffset = texture.ImagePlacement,
                MipMapCount = texture.NumMipMapLevels,
                TexHash = texture.Hash,
                TypeHash = texture.ClassNameHash,
                Format = 0,
                PitchOrLinearSize = texture.BaseImageSize,
            });

            return _texturePack.Textures[_texturePack.Textures.Count - 1];
        }
    }
}