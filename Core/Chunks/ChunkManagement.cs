using System.Text;
using Core.Solids;
using Core.Textures;
using Core.Scenery;
using System.Collections;
using Core.Game.UG2;
using Core.Game.Underground;
using Core.Game.MostWanted;


namespace Core
{
    public abstract class RealFile
    {
        protected const uint TexturePackChunk = 0xb3300000;
        protected const uint ObjectPackChunk = 0x80134000;

        protected Stream _stream;
        protected BinaryReader _br;
        protected BinaryWriter _bw;
        protected Stack _chunkStack;

        protected void NextAlignment(int alignment)
        {
            if (_stream.Position % alignment != 0)
            {
                _stream.Position += alignment - _stream.Position % alignment;
            }
        }

        protected void SkipChunk(RealChunk chunk)
        {
            chunk.Skip(_stream);
        }

        protected RealChunk NextChunk()
        {
            var chunk = new RealChunk();
            chunk.Read(_br);
            return chunk;
        }


        protected RealChunk BeginChunk(uint type)
        {
            var chunk = new RealChunk
            {
                Offset = _stream.Position,
                Type = type
            };

            _chunkStack.Push(chunk);
            _stream.Seek(0x8, SeekOrigin.Current);
            return chunk;
        }

        protected void EndChunk()
        {
            if (_chunkStack.Pop() is RealChunk chunk)
            {
                chunk.EndOffset = (int)_stream.Position;
                chunk.Write(_bw);
            }
        }

        public void Open(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            Open(fs);
        }

        public void Save(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            _stream = fs;
            _bw = new BinaryWriter(fs);
            _chunkStack = new Stack();
            ProcessSave();
            Close();
        }

        public void Open(Stream stream)
        {
            if (_stream != null)
                Close();

            _stream = stream;
            _stream.Seek(0, SeekOrigin.Begin);
            _br = new BinaryReader(_stream, Encoding.Default, true);
            ProcessOpen();
            Close();
        }

        private void Close()
        {
            //_br?.Close();
            //_bw?.Close();
            _br?.Dispose();
            _bw?.Dispose();
            _chunkStack = null;
            _br = null;
            _bw = null;
            _stream.Dispose();
        }

        protected abstract void ProcessOpen();
        protected abstract void ProcessSave();
    }

    public class ChunkManager : RealFile
    {
        public List<Chunk> Chunks { get; } = new();

        private readonly GameDetector.Game _game;

        public ChunkManager(GameDetector.Game game)
        {
            _game = game;
        }

        protected override void ProcessOpen()
        {
            while (_stream.Position < _stream.Length)
            {
                var chunk = NextChunk();

                var padding = 0u;
                var cd = new Chunk();

                if (Chunks.Count > 0)
                {
                    if (Chunks[^1].Id == 0)
                    {
                        cd.PrePadding = Chunks[^1].Size;
                    }
                }

                while (_br.BaseStream.Position < _br.BaseStream.Length && _br.ReadUInt32() == 0x11111111)
                {
                    padding += 4;
                }

                _br.BaseStream.Position -= 4;

                cd.Padding = padding;

                cd.Id = chunk.Type;
                cd.Size = chunk.Length - padding;
                cd.Data = Array.Empty<byte>();
                cd.Offset = chunk.Offset;
                cd.SubChunks = new List<Chunk>();

                switch (chunk.Type)
                {
                    case ObjectPackChunk:
                        {
                            SolidListManager solidListManager = _game switch
                            {
                                GameDetector.Game.Underground => new UndergroundSolids(),
                                GameDetector.Game.Underground2 => new UG2Solids(),
                                GameDetector.Game.MostWanted => new MostWantedSolids(),
                                _ => throw new Exception($"Cannot process solid list chunk for game: {_game}")
                            };

                            cd.Resource = solidListManager.ReadSolidList(_br, chunk.Length);

                            break;
                        }
                    case TexturePackChunk:
                        {
                            TpkManager tpkManager = _game switch
                            {
                                GameDetector.Game.Underground => new Version1Tpk(),
                                GameDetector.Game.Underground2 => new Version1Tpk(),
                                GameDetector.Game.MostWanted => new Version1Tpk(),
                                _ => throw new Exception($"Cannot process TPK chunk for game: {_game}")
                            };

                            cd.Resource = tpkManager.ReadTexturePack(_br, chunk.Length);
                            break;
                        }
                    case 0x80034100:
                        {
                            SceneryManager sceneryManager = _game switch
                            {
                                GameDetector.Game.Underground => new UndergroundScenery(),
                                GameDetector.Game.Underground2 => new Underground2Scenery(),
                                GameDetector.Game.MostWanted => new MostWantedScenery(),
                                _ => throw new Exception($"Cannot process scenery chunk for game: {_game}")
                            };

                            cd.Resource = sceneryManager.ReadScenery(_br, chunk.Length);

                            break;
                        }
                    default:
                        // If the chunk is a container chunk, read its sub-chunks.
                        if (chunk.IsParent)
                        {
                            ReadSubChunks(cd.SubChunks, cd.Size);
                        }
                        else
                        {
                            cd.Data = new byte[cd.Size];
                            _br.Read(cd.Data, 0, cd.Data.Length);
                        }

                        break;
                }

                Chunks.Add(cd);

                SkipChunk(chunk);
            }
        }

        private void ReadSubChunks(ICollection<Chunk> chunkList, uint length)
        {
            var endPos = _stream.Position + length;

            while (_stream.Position < endPos)
            {
                var chunk = NextChunk();

                if (chunk.IsParent)
                {
                    var master = new Chunk
                    {
                        Id = chunk.Type,
                        Offset = chunk.Offset,
                        Size = chunk.Length,
                        SubChunks = new List<Chunk>()
                    };

                    var padding = 0u;

                    while (_br.ReadUInt32() == 0x11111111)
                    {
                        padding += 4;
                    }

                    _br.BaseStream.Position -= 4;

                    master.Padding = padding;
                    master.Size -= padding;

                    ReadSubChunks(master.SubChunks, master.Size);

                    chunkList.Add(master);
                }
                else
                {
                    var padding = 0u;

                    while (_br.ReadUInt32() == 0x11111111)
                    {
                        padding += 4;
                    }

                    _br.BaseStream.Position -= 4;

                    var child = new Chunk
                    {
                        Padding = padding,
                        Id = chunk.Type,
                        Offset = chunk.Offset,
                        Size = chunk.Length - padding,
                        SubChunks = new List<Chunk>(),
                        Data = _br.ReadBytes((int)chunk.Length)
                    };

                    chunkList.Add(child);
                }

                SkipChunk(chunk);
            }
        }

        protected override void ProcessSave()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read chunks from a file.
        /// </summary>
        /// <param name="path"></param>
        public void Read(string path)
        {
            Open(path);
        }

        /// <summary>
        /// Read chunks from a binary stream.
        /// </summary>
        /// <param name="br"></param>
        public void Read(BinaryReader br)
        {
            Open(br.BaseStream);
        }

        /// <summary>
        /// Resets the chunk manager.
        /// </summary>
        public void Reset() => Chunks.Clear();
    }
    //WIP
    //Chunk_Defs
    /*
     0xb3300000 BCHUNK_SPEED_TEXTURE_PACK_LIST_CHUNKS
0xb0300100 BCHUNK_SPEED_TEXTURE_PACK_LIST_CHUNKS_ANIM
0x80134000 BCHUNK_SPEED_ESOLID_LIST_CHUNKS
0x80034100 BCHUNK_SPEED_SCENERY_SECTION
0x00034027 BCHUNK_SPEED_SMOKEABLE_SPAWNER
0x00034110 BCHUNK_TRACKSTREAMER_0
0x00034111 BCHUNK_TRACKSTREAMER_1
0x00034112 BCHUNK_TRACKSTREAMER_2
0x00034113 BCHUNK_TRACKSTREAMER_3
0x00034107 BCHUNK_TRACKSTREAMER_7
0x00037260 BCHUNK_SPEED_BBGANIM_INSTANCE_TREE
0x00037250 BCHUNK_SPEED_BBGANIM_INSTANCE_NODE
0x00037270 BCHUNK_SPEED_BBGANIM_ENDPACKHEADER
0x80135000 BCHUNK_SPEED_ELIGHT_CHUNKS
0x80036000 BCHUNK_SPEED_EMTRIGGER_PACK
0x00037220 BCHUNK_SPEED_BBGANIM_BLOCKHEADER
0x0003bc00 BCHUNK_SPEED_EMITTER_LIBRARY
0x00030201 BCHUNK_FENG_FONT
0x00030210 BCHUNK_FENG_PACKAGE_COMPRESSED
0x00030203 BCHUNK_FENG_PACKAGE
0x00135200 BCHUNK_ELIGHTS
0x00034600 BCHUNK_CARINFO_ARRAY
0x00034601 BCHUNK_CARINFO_SKININFO
0x00034608 BCHUNK_CARINFO_ANIMHOOKUPTABLE
0x00034609 BCHUNK_CARINFO_ANIMHIDETABLES
0x00034607 BCHUNK_CARINFO_SLOTTYPES
0x80034602 BCHUNK_CARINFO_CARPART
0x00034201 BCHUNK_TRACKINFO
0x00034202 BCHUNK_SUN
0x80035000 BCHUNK_ACIDFX
0x80035010 BCHUNK_ACIDFX
0x00035021 BCHUNK_ACIDFX
0x00035020 BCHUNK_ACIDFX_EMITTER
0x00034b00 BCHUNK_DIFFICULTYINFO
0x00034a07 BCHUNK_STYLEMOMENTSINFO
0x00030220 BCHUNK_FEPRESETCARS
0x00e34009 BCHUNK_EAGLSKELETONS
0x00e34010 BCHUNK_EAGLANIMATIONS
0x00039020 BCHUNK_MOVIECATALOG
0x8003b900 BCHUNK_BOUNDS
0x0003bd00 BCHUNK_EMITTERSYSTEM_TEXTUREPAGE
0xb0300300 BCHUNK_PCAWEIGHTS
0x30300201 BCHUNK_COLORCUBE
0x80037050 BCHUNK_ANIMDIRECTORYDATA
0x8003b200 BCHUNK_ICECAMERASET
0x8003B201 BCHUNK_ICECAMERASET
0x8003b202 BCHUNK_ICECAMERASET
0x8003b203 BCHUNK_ICECAMERASET
0x8003b500 BCHUNK_SOUNDSTICHS
0x80034147 BCHUNK_TRACKPATH
0x00034146 BCHUNK_TRACKPOSITIONMARKERS
0x00034158 BCHUNK_VISIBLESECTION
0x80034150 BCHUNK_VISIBLESECTION
0x00034250 BCHUNK_WEATHERMAN
0x8003b000 BCHUNK_QUICKSPLINE
0x8003b600 BCHUNK_PARAMETERMAPS
0x80034100 BCHUNK_SPEED_SCENERY_SECTION
0x00034108 BCHUNK_SCENERY
0x00034109 BCHUNK_SCENERYGROUP
0x8003410b BCHUNK_SCENERY
0x0003b800 BCHUNK_WWORLD
0x0003b801 BCHUNK_CARP_WCOLLISIONPACK
0x8003b810 BCHUNK_EVENTSEQUENCE
0x0003414d BCHUNK_TRACKPATH
0x00037080 BCHUNK_WORLDANIMENTITYDATA
0x00037110 BCHUNK_WORLDANIMTREEMARKER
0x00037150 BCHUNK_WORLDANIMINSTANCEENTRY
0x00037090 BCHUNK_WORLDANIMDIRECTORYDATA
0x30300200 BCHUNK_DDSTEXTURE
0x0003ce12 BCHUNK_SKINREGIONDATABASE
0x0003ce13 BCHUNK_VINYLMETADATA
0x0003b200 BCHUNK_ICECAMERAS
0x00039000 BCHUNK_LANGUAGE
0x00039001 BCHUNK_LANGUAGEHISTOGRAM
0x00034a08 BCHUNK_STYLEREWARDCHUNK
0x00030230 BCHUNK_MAGAZINES
0x00034026 BCHUNK_SMOKEABLES
0x00034492 BCHUNK_CAMERA
0x80034405 BCHUNK_CAMERA
0x80034425 BCHUNK_CAMERA
0x80034410 BCHUNK_CAMERA
0x80034415 BCHUNK_CAMERA
0x80034420 BCHUNK_CAMERA
0x0003a000 BCHUNK_ELIPSE_TABLE
0x00034036 BCHUNK_NIS_SCENE_MAPPER_DATA
0x00034121 BCHUNK_TRACKROUTE_MANAGER
0x00034122 BCHUNK_TRACKROUTE_SIGNPOSTS
0x00034123 BCHUNK_TRACKROUTE_TRAFFIC_INTERSECTIONS
0x00034124 BCHUNK_TRACKROUTE_CROSS_TRAFFIC_EMITTERS
0x00034130 BCHUNK_TOPOLOGYTREE
0x00034131 BCHUNK_TOPOLOGYTREE
0x00034132 BCHUNK_TOPOLOGYTREE
0x00034133 BCHUNK_TOPOLOGYTREE
0x00034134 BCHUNK_TOPOLOGYTREE
0x0003b300 BCHUNK_WORLDOBJECTS
0x00034a09 BCHUNK_PERFUPGRADELEVELINFOCHUNK
0x00034a0a BCHUNK_PERFUPGRADEPACKAGECHUNK
0x00030240 BCHUNK_WIDEDECALS
0x00034a03 BCHUNK_RANKINGLADDERS
0x00039010 BCHUNK_SUBTITLES
0x00034035 BCHUNK_NISSCENEDATA
0x80037020 BCHUNK_ANIMSCENEDATA
0x0003b811 BCHUNK_EVENTSEQUENCE
0x80034020 BCHUNK_COLLISION_VOLUMES
    */
}
