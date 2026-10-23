namespace DatExplorerIII.Models.Graphics
{
	using System;

	/// <summary>
	/// A mask of one bit per pixel (.msk): the pixels that are set and the pixels that are not, line after line.
	/// </summary>
	public sealed record MaskImage(Int32 Width, Int32 Height, Boolean[] Pixels)
	{
		/// <summary>How many pixels are set.</summary>
		public Int32 SetCount
		{
			get
			{
				Int32 count = 0;
				foreach (Boolean pixel in Pixels)
				{
					if (pixel)
						count++;
				}

				return count;
			}
		}
	}
}