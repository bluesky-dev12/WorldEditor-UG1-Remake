﻿using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Core.Scenery.Data;

namespace Core.Scenery
{
    public class Underground2Scenery : SceneryManager
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ScenerySectionHeader // 0x00034101
        {
            public long Pointer1;
            public int Pointer2;
            public int SectionNumber;
            public int Pointer3;
            public long Pointer4;
            public long Pointer5;
            public long Pointer6;
            public long Pointer7;
            public long Pointer8;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SceneryInfoStruct // 0x00034102
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string Name;

            public uint SolidMeshKey1;
            public uint SolidMeshKey2;
            public uint SolidMeshKey3;
            public ushort SomeFlag1;
            public ushort SomeFlag2;
            public int Pointer1;
            public int Pointer2;
            public int Pointer3;
            public float Radius;
            public uint HierarchyKey;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SceneryInstanceInternal // 0x00034103
        {
            public Vector3 BBoxMin;
            public Vector3 BBoxMax;
            public ushort SceneryInfoNumber;
            public ushort InstanceFlags;
            public int PrecullerInfoIndex;
            public Vector3 Position;
            public PackedRotationMatrix Rotation;
            public ushort Padding;
        }

        private ScenerySection _scenerySection;

        public override ScenerySection ReadScenery(BinaryReader br, uint containerSize)
        {
            _scenerySection = new ScenerySection();
            ReadChunks(br, containerSize);
            return _scenerySection;
        }

        protected override void ReadChunks(BinaryReader br, uint containerSize)
        {
            var endPos = br.BaseStream.Position + containerSize;

            while (br.BaseStream.Position < endPos)
            {
                var chunkId = br.ReadUInt32();
                var chunkSize = br.ReadUInt32();
                var chunkEndPos = br.BaseStream.Position + chunkSize;
                //if ((chunkId & 0x80000000) != 0x80000000)
                //{

                //}

                var padding = 0u;

                while (br.ReadUInt32() == 0x11111111)
                {
                    padding += 4;

                    if (br.BaseStream.Position >= chunkEndPos)
                    {
                        break;
                    }
                }

                br.BaseStream.Position -= 4;
                chunkSize -= padding;

                switch (chunkId)
                {
                    case 0x00034101:
                        {
                            ReadScenerySectionHeader(br);
                            break;
                        }
                    case 0x00034102:
                        {
                            ReadSceneryInfos(br, chunkSize);
                            break;
                        }
                    case 0x00034103:
                        {
                            ReadSceneryInstances(br, chunkSize);
                            break;
                        }
                    default:
                        //Console.WriteLine($"0x{chunkId:X8} [{chunkSize}] @{br.BaseStream.Position}");
                        break;
                }

                br.BaseStream.Position = chunkEndPos;
            }
        }

        private void ReadScenerySectionHeader(BinaryReader br)
        {
            var header = BinaryUtil.ReadUnmanagedStruct<ScenerySectionHeader>(br);
            _scenerySection.SectionNumber = header.SectionNumber;
            //Debug.Log($"ScenerySection number is {_scenerySection.SectionNumber}");
        }

        private void ReadSceneryInfos(BinaryReader br, uint size)
        {
            Debug.Assert(size % 0x44 == 0);
            var count = (int)size / 0x44;
            _scenerySection.Infos = new List<SceneryInfo>(count);
            for (int i = 0; i < count; ++i)
            {
                var info = BinaryUtil.ReadStruct<SceneryInfoStruct>(br);
                _scenerySection.Infos.Add(new SceneryInfo
                {
                    Name = info.Name,
                    SolidKey = info.SolidMeshKey1
                });
            }
            //Debug.Log($"Loaded {_scenerySection.SceneryInfos.Count} scenery definitions for ScenerySection {_scenerySection.SectionNumber}");
        }

        private void ReadSceneryInstances(BinaryReader br, uint size)
        {
            size -= BinaryUtil.AlignReader(br, 0x10);
            Debug.Assert(size % 0x40 == 0);
            var count = (int)size / 0x40;
            _scenerySection.Instances = new List<SceneryInstance>(count);

            for (int i = 0; i < count; ++i)
            {
                var instance = new SceneryInstance();
                var internalInstance = BinaryUtil.ReadUnmanagedStruct<SceneryInstanceInternal>(br);

                instance.InfoIndex = internalInstance.SceneryInfoNumber;
                instance.Transform = Matrix4x4.Multiply(internalInstance.Rotation,
                    Matrix4x4.CreateTranslation(internalInstance.Position));

                _scenerySection.Instances.Add(instance);
            }
            //Debug.Log($"Loaded {_scenerySection.SceneryInstances.Count} scenery instances for ScenerySection {_scenerySection.SectionNumber}");
        }
    }
}