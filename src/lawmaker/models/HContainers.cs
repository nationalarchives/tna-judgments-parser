
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    interface IHContainer : IDivision
    {

        string Class { get; }

        public bool HeadingPrecedesNumber { get; }

    }

    abstract class HContainer : IHContainer
    {

        public abstract string Name { get; internal init; }

        public abstract string Class { get; }

        public virtual IFormattedText Number { get; internal set; }

        public virtual ILine Heading { get; internal set; }

        public virtual bool HeadingPrecedesNumber { get; } = false;

        public virtual string FirstText()
        {
            if (!HeadingPrecedesNumber && Number != null)
                return Number.Text;
            if (Heading == null)
                return null;
            string contents = IInline.ToString(Heading.Contents);
            return Regex.Replace(contents, @"\s+", " ").Trim();
        }

    }

    abstract class Branch : HContainer, IBranch
    {

        public IList<IBlock> Intro { get; internal init; }

        public IList<IDivision> Children { get; internal init; }

        public IList<IBlock> WrapUp { get; internal init; }

        IEnumerable<IBlock> IBranch.Intro => Intro;

        IEnumerable<IDivision> IBranch.Children => Children;

        IEnumerable<IBlock> IBranch.WrapUp => WrapUp;

        public override string FirstText()
        {
            string baseText = base.FirstText();
            if (baseText != null)
                return baseText;
            if (Intro == null)
                return null;
            IBlock firstLine = Intro.FirstOrDefault(b => b is ILine);
            if (firstLine == null)
                return null;
            string contents = IInline.ToString((firstLine as ILine).Contents);
            return Regex.Replace(contents, @"\s+", " ").Trim();
        }

    }

    abstract class Leaf : HContainer, ILeaf
    {

        public IList<IBlock> Contents { get; internal init; }

        IEnumerable<IBlock> ILeaf.Contents => Contents;

        public override string FirstText()
        {
            string baseText = base.FirstText();
            if (baseText != null)
                return baseText;
            if (Contents == null)
                return null;
            IBlock firstLine = Contents.FirstOrDefault(b => b is ILine);
            if (firstLine == null)
                return null;
            string contents = IInline.ToString((firstLine as ILine).Contents);
            return Regex.Replace(contents, @"\s+", " ").Trim();
        }

    }

}
