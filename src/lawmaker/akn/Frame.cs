
using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Lawmaker
{


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
            // Frames are primarily used to track quoted structures, because quoted structures are never quoting draft legislation,
            // we want to ensure the default DocName is the enacted type of the document we're parsing.
            defaultDocName = DocNames.ToEnacted(docName);
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
            return IsSecondaryDocName(CurrentDocName);
        }

        public static bool IsSecondaryDocName(DocName docName)
        {
            return DocNames.GetLegislationType(docName) == LegislationType.SECONDARY;
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
