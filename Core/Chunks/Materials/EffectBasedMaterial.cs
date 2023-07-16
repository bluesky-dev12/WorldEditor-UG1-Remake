using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Materials
{
    public class EffectBasedMaterial : SolidObjectMaterial
    {
        public uint EffectId { get; set; }
    }
}