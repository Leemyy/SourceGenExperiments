using System;

namespace Experiments
{
	public record Parent {
		public string? X { get; init; }
		public string? Y { get; init; }

		public override string ToString() {
			return "<" + Y + "|" + X + ">";
		}
	}

	public partial record Alpha : Parent
	{
		public string? A { get; init; }
	}
}

namespace Experiments.Child
{
	public partial record Beta : Parent
	{
		public string? B { get; init; }
	}
}
