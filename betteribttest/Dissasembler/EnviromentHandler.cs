using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.Dissasembler
{
    public sealed class EnviromentHandler
    {

        Instruction Start { get; set; }
        Instruction End { get; set; }
        Instruction Parent { get; set; }
    }
}
