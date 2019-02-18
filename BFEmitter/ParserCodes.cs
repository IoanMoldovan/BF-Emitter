using System;
using System.Reflection.Emit;

namespace BFEmitter
{
    public class ParserCodes
    {
        public interface IParserCode { }

        public class ArithmeticOp : IParserCode
        {
            public int Value { get; set; }
        }

        public class ArithmeticPointerOp : IParserCode
        {
            public int Value { get; set; }
        }

        public class WriteOp : IParserCode { }
        public class ReadOp : IParserCode { }

        public class CompBeginOp : IParserCode
        {
            public Guid Tag { get; set; }
            public Label Label { get; set; }
            public Label OnFail { get; set; }
        }

        public class CompEndOp : IParserCode
        {
            public Guid Tag { get; set; }
            public Label Label { get; set; }
            public Label OnFail { get; set; }
        }
    }
}
