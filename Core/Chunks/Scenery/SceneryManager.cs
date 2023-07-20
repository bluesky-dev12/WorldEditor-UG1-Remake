using System.IO;
using Core.Scenery.Data;

namespace Core.Scenery
{
    public abstract class SceneryManager
    {
        protected SceneryManager() { }

        /// <summary>
        /// Read a scenery pack from the given binary stream.
        /// </summary>
        /// <param name="br"></param>
        /// <param name="containerSize"></param>
        /// <returns></returns>
        public abstract ScenerySection ReadScenery(BinaryReader br, uint containerSize);

        protected abstract void ReadChunks(BinaryReader br, uint containerSize);
    }
}