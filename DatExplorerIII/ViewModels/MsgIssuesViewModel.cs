namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>A problem of a message file, as the list shows it.</summary>
	public sealed record MsgIssueRow(String File, MsgIssueSeverity Severity, String SeverityText, Int32 Line, Int32 Column, String Message, String Excerpt);

	/// <summary>A file of the list on the left: its name, and the problems it has (those whose severity is shown).</summary>
	public sealed record MsgFileItem(String Path, IReadOnlyList<MsgIssueRow> Issues)
	{
		/// <summary>The name of the file, without its folder.</summary>
		public String Name => System.IO.Path.GetFileName(Path);

		/// <summary>The folder of the file (what is left of its path).</summary>
		public String Folder => System.IO.Path.GetDirectoryName(Path) ?? String.Empty;

		public Int32 Count => Issues.Count;

		/// <summary>The most serious problem of the file, which tells the color of its mark.</summary>
		public MsgIssueSeverity Worst => Issues.Min(issue => issue.Severity);
	}

	/// <summary>
	/// The problems of message files: the files that have some on the left, and on the right the problems of the selected file one
	/// at a time (first, previous, next, last) with their severity, the error and the line itself, both texts being there to
	/// copy. The severities can be hidden.
	/// </summary>
	public sealed class MsgIssuesViewModel : ObservableObject
	{
		private readonly IReadOnlyList<MsgIssueRow> _all;
		private readonly MsgReportText _text;
		private readonly IUiThread _uiThread;
		private IReadOnlyList<MsgFileItem> _files = Array.Empty<MsgFileItem>();
		private MsgFileItem? _selectedFile;
		private Int32 _position;
		private Boolean _showCritical = true;
		private Boolean _showHigh = true;
		private Boolean _showMedium = true;

		public MsgIssuesViewModel(IReadOnlyList<MsgFileReport> reports, MsgReportText text, IUiThread uiThread)
		{
			_text = text;
			_uiThread = uiThread;
			_all = reports
				.SelectMany(report => report.Issues.Select(issue => new MsgIssueRow(report.Path, issue.Severity, text.SeverityName(issue.Severity), issue.Line, issue.Column, text.Describe(issue), issue.Excerpt)))
				.ToList();

			FirstCommand = new RelayCommand(() => Position = 0, () => _position > 0);
			PreviousCommand = new RelayCommand(() => Position = _position - 1, () => _position > 0);
			NextCommand = new RelayCommand(() => Position = _position + 1, () => _selectedFile != null && _position < _selectedFile.Count - 1);
			LastCommand = new RelayCommand(() => Position = (_selectedFile?.Count ?? 1) - 1, () => _selectedFile != null && _position < _selectedFile.Count - 1);
			CopyMessageCommand = new RelayCommand(() => _uiThread.SetClipboardText(CurrentMessage), () => CurrentIssue != null);
			CopyValueCommand = new RelayCommand(() => _uiThread.SetClipboardText(CurrentValue), () => CurrentIssue != null);
			Rebuild(null);
		}

		public ICommand FirstCommand { get; }

		public ICommand PreviousCommand { get; }

		public ICommand NextCommand { get; }

		public ICommand LastCommand { get; }

		/// <summary>Copies the text of the error that is shown.</summary>
		public ICommand CopyMessageCommand { get; }

		/// <summary>Copies the line of the file that is shown.</summary>
		public ICommand CopyValueCommand { get; }

		/// <summary>The files that have problems of a severity that is shown, sorted by the worst problem, then by path.</summary>
		public IReadOnlyList<MsgFileItem> Files
		{
			get => _files;
			private set
			{
				if (SetProperty(ref _files, value))
				{
					OnPropertyChanged(nameof(HasFiles));
				}
			}
		}

		public Boolean HasFiles => _files.Count > 0;

		public MsgFileItem? SelectedFile
		{
			get => _selectedFile;
			set
			{
				if (SetProperty(ref _selectedFile, value))
				{
					_position = 0;
					OnCurrentChanged();
				}
			}
		}

		/// <summary>The problem that is shown, among those of the selected file (from 0).</summary>
		public Int32 Position
		{
			get => _position;
			set
			{
				Int32 last = Math.Max(0, (_selectedFile?.Count ?? 1) - 1);
				Int32 position = Math.Clamp(value, 0, last);
				if (position != _position)
				{
					_position = position;
					OnCurrentChanged();
				}
			}
		}

		/// <summary>"3 / 13": which problem of the file is shown.</summary>
		public String PositionText => _selectedFile == null ? String.Empty : (_position + 1) + " / " + _selectedFile.Count;

		public MsgIssueRow? CurrentIssue => _selectedFile == null ? null : _selectedFile.Issues[Math.Min(_position, _selectedFile.Count - 1)];

		public Boolean HasCurrent => CurrentIssue != null;

		public MsgIssueSeverity CurrentSeverity => CurrentIssue?.Severity ?? MsgIssueSeverity.Medium;

		public String CurrentSeverityText => CurrentIssue?.SeverityText ?? String.Empty;

		/// <summary>"Line 34, column 5".</summary>
		public String CurrentLocation => CurrentIssue is { } issue ? Loc.Format("Str.MsgLocation", issue.Line, issue.Column) : String.Empty;

		/// <summary>What is wrong, in words.</summary>
		public String CurrentMessage => CurrentIssue?.Message ?? String.Empty;

		/// <summary>The line of the file where it is wrong.</summary>
		public String CurrentValue => CurrentIssue?.Excerpt ?? String.Empty;

		public Boolean ShowCritical
		{
			get => _showCritical;
			set => SetFilter(ref _showCritical, value);
		}

		public Boolean ShowHigh
		{
			get => _showHigh;
			set => SetFilter(ref _showHigh, value);
		}

		public Boolean ShowMedium
		{
			get => _showMedium;
			set => SetFilter(ref _showMedium, value);
		}

		/// <summary>"13 problems, 4 critical, 8 high, 1 medium": all the problems, whatever the filters.</summary>
		public String Summary => MsgIssueSummary.Describe(CountOf(MsgIssueSeverity.Critical), CountOf(MsgIssueSeverity.High), CountOf(MsgIssueSeverity.Medium));

		public String CriticalText => _text.SeverityName(MsgIssueSeverity.Critical) + " (" + CountOf(MsgIssueSeverity.Critical) + ")";

		public String HighText => _text.SeverityName(MsgIssueSeverity.High) + " (" + CountOf(MsgIssueSeverity.High) + ")";

		public String MediumText => _text.SeverityName(MsgIssueSeverity.Medium) + " (" + CountOf(MsgIssueSeverity.Medium) + ")";

		private Int32 CountOf(MsgIssueSeverity severity) => _all.Count(row => row.Severity == severity);

		private Boolean IsShown(MsgIssueSeverity severity)
		{
			return severity switch
			{
				MsgIssueSeverity.Critical => _showCritical,
				MsgIssueSeverity.High => _showHigh,
				_ => _showMedium,
			};
		}

		private void SetFilter(ref Boolean field, Boolean value)
		{
			if (field == value)
			{
				return;
			}

			field = value;
			Rebuild(_selectedFile?.Path);
			OnPropertyChanged(nameof(ShowCritical));
			OnPropertyChanged(nameof(ShowHigh));
			OnPropertyChanged(nameof(ShowMedium));
		}

		/// <summary>Makes the list of the files again after a filter changed; the file that was selected stays selected when it is still there.</summary>
		private void Rebuild(String? keep)
		{
			List<MsgFileItem> files = _all
				.Where(row => IsShown(row.Severity))
				.GroupBy(row => row.File)
				.Select(group => new MsgFileItem(group.Key, group.OrderBy(row => row.Line).ThenBy(row => row.Column).ToList()))
				.OrderBy(file => file.Worst)
				.ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
				.ToList();

			Files = files;
			SelectedFile = files.FirstOrDefault(file => file.Path == keep) ?? files.FirstOrDefault();
			OnCurrentChanged();
		}

		private void OnCurrentChanged()
		{
			OnPropertyChanged(nameof(Position));
			OnPropertyChanged(nameof(PositionText));
			OnPropertyChanged(nameof(CurrentIssue));
			OnPropertyChanged(nameof(HasCurrent));
			OnPropertyChanged(nameof(CurrentSeverity));
			OnPropertyChanged(nameof(CurrentSeverityText));
			OnPropertyChanged(nameof(CurrentLocation));
			OnPropertyChanged(nameof(CurrentMessage));
			OnPropertyChanged(nameof(CurrentValue));
			CommandRequery.Invalidate();
		}
	}
}