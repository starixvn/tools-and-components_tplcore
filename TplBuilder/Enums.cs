using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TplBuilder
{
    public enum MappingErrorType : byte
    {
        None,
        MissingSource,
        MissingDestination
    };
}
