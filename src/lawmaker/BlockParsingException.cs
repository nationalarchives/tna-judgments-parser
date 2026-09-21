
using System;

namespace UK.Gov.Legislation.Lawmaker;

public class BlockParsingException : Exception
{

    public int BlockNumber { get; }

    public string BlockText { get; }

    public BlockParsingException(int blockNumber, string blockText, Exception inner)
        : base($"error parsing block {blockNumber}", inner)
    {
        BlockNumber = blockNumber;
        BlockText = blockText;
    }

}
