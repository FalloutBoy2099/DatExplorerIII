namespace DatExplorerIII.ViewModels
{
	using System;
	using Models;
	using Services;

	/// <summary>
	/// Asks what to do when a file already exists, and remembers the answer when the user says "to all".
	/// One resolver serves one operation.
	/// </summary>
	public sealed class ConflictResolver
	{
		private readonly IDialogService _dialogs;
		private readonly String _title;
		private ConflictDecision? _always;

		public ConflictResolver(IDialogService dialogs, String title)
		{
			_dialogs = dialogs;
			_title = title;
		}

		public ConflictDecision Resolve(String message)
		{
			if (_always is { } remembered)
			{
				return remembered;
			}

			DialogButton[] buttons =
			[
				new("yes", Loc.Get("Str.Yes"), IsDefault: true),
				new("yesAll", Loc.Get("Str.YesToAll")),
				new("no", Loc.Get("Str.No")),
				new("noAll", Loc.Get("Str.NoToAll")),
				new("cancel", Loc.Get("Str.Cancel"), IsCancel: true),
			];

			switch (_dialogs.Choose(_title, message, DialogIcon.Question, buttons))
			{
				case "yes":
					return ConflictDecision.Replace;
				case "yesAll":
					_always = ConflictDecision.Replace;
					return ConflictDecision.Replace;
				case "no":
					return ConflictDecision.Skip;
				case "noAll":
					_always = ConflictDecision.Skip;
					return ConflictDecision.Skip;
				default:
					return ConflictDecision.Cancel;
			}
		}
	}
}