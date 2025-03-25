
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Lawmaker
{

    public enum DocName { NIA, UKPGA, ASP, NISI, NISR, UKSI, SSI }

    public enum Context { BODY, SCH, ORDER, RULES, REGS }


    class Frames
    {

        private Stack<Frame> frames;
        private DocName defaultDocName;
        private Context defaultContext;

        public DocName CurrentDocName => frames.Count > 0 ? frames.Peek().DocName : defaultDocName;
        public Context CurrentContext => frames.Count > 0 ? frames.Peek().Context : defaultContext;

        
        public Frames(DocName docName, Context context)
        {
            defaultDocName = docName;
            defaultContext = context;
            frames = new Stack<Frame>();
            frames.Push(new Frame(docName, context));
        }

        public void Push(DocName docName, Context context)
        {
            frames.Push(new Frame(docName, context));
        }

        public void PushDefault()
        {
            frames.Push(new Frame(defaultDocName, defaultContext));
        }

        public void PushScheduleContext()
        {
            frames.Push(new Frame(CurrentDocName, Context.SCH));
        }

        public bool IsScheduleContext()
        {
            return CurrentContext == Context.SCH;
        }

        public bool IsSecondaryDocName()
        {
            return new[] { DocName.NISI, DocName.NISR, DocName.UKSI, DocName.SSI }.Contains(CurrentDocName);
        }

        public bool Pop()
        {
            if (frames.Count == 0)
                return false;
            frames.Pop();
            return true; 
        }

        private class Frame
        {
            public DocName DocName { get; }
            public Context Context { get; }

            public Frame(DocName docName, Context context)
            {
                DocName = docName;
                Context = context;
            }

        }

    }

}
