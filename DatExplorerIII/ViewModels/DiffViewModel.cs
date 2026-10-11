namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Core;
	using Infrastructure;
	using Services;

	/// <summary>
	/// The differences between the two versions of a file, as the two texts side by side with what differs marked, and the
	/// way to go from one difference to the next. When there is nothing to show it says why.
	/// </summary>
	public sealed class DiffViewModel : ObservableObject
	{
		private List<DiffRow> _list = [];
		private IReadOnlyList<DiffRow> _rows = [];
		private IReadOnlyList<Int32> _starts = [];
		private DiffRow? _selectedRow;
		private String _message = String.Empty;
		private String _summary = String.Empty;
		private String _messageKey = String.Empty;
		private Int32 _differenceCount;
		private Boolean _exact = true;
		private IReadOnlyList<DiffNote> _notes = [];

		public DiffViewModel()
		{
			NextCommand = new RelayCommand(() => Go(true), () => _starts.Count > 0);
			PreviousCommand = new RelayCommand(() => Go(false), () => _starts.Count > 0);
		}

		public ICommand NextCommand { get; }

		public ICommand PreviousCommand { get; }

		/// <summary>The lines of the two texts side by side.</summary>
		public IReadOnlyList<DiffRow> Rows
		{
			get => _rows;
			private set
			{
				if (SetProperty(ref _rows, value))
				{
					OnPropertyChanged(nameof(HasRows));
				}
			}
		}

		/// <summary>The line the user is on (the list shows it; "next difference" starts from it).</summary>
		public DiffRow? SelectedRow
		{
			get => _selectedRow;
			set => SetProperty(ref _selectedRow, value);
		}

		/// <summary>What there is to say instead of the differences (no file selected, the same file, a picture...); empty when they are shown.</summary>
		public String Message
		{
			get => _message;
			private set
			{
				if (SetProperty(ref _message, value))
				{
					OnPropertyChanged(nameof(HasMessage));
					OnPropertyChanged(nameof(HasRows));
				}
			}
		}

		public Boolean HasMessage => _message.Length > 0;

		public Boolean HasRows => _message.Length == 0 && _rows.Count > 0;

		/// <summary>How many groups of lines differ, and whether the comparison was line by line.</summary>
		public String Summary
		{
			get => _summary;
			private set => SetProperty(ref _summary, value);
		}

		public Int32 DifferenceCount
		{
			get => _differenceCount;
			private set => SetProperty(ref _differenceCount, value);
		}

		/// <summary>Shows nothing but a sentence (the key of its text).</summary>
		public void ShowMessage(String key)
		{
			_messageKey = key;
			_starts = [];
			_list = [];
			Rows = _list;
			SelectedRow = null;
			DifferenceCount = 0;
			Summary = String.Empty;
			Message = Loc.Get(key);
			CommandManager.InvalidateRequerySuggested();
		}

		/// <summary>
		/// Shows the lines side by side; <paramref name="exact"/> is false when the texts were too different to be matched line by
		/// line. <paramref name="notes"/> tell what else differs (the line endings...).
		/// </summary>
		public void Show(IReadOnlyList<DiffRow> rows, Boolean exact, IReadOnlyList<DiffNote>? notes = null)
		{
			_exact = exact;
			_notes = notes ?? [];
			_messageKey = String.Empty;
			_starts = TextDiff.GroupStarts(rows);
			_list = rows as List<DiffRow> ?? rows.ToList();
			Message = String.Empty;
			Rows = _list;
			DifferenceCount = _starts.Count;
			SelectedRow = _starts.Count > 0 ? _list[_starts[0]] : null;
			Summary = BuildSummary();
			CommandManager.InvalidateRequerySuggested();
		}

		/// <summary>The number of differences, then what else there is to say about the two texts.</summary>
		private String BuildSummary()
		{
			List<String> parts = [Loc.Format("Str.CompareDiffCount", _starts.Count)];
			if (!_exact)
			{
				parts.Add(Loc.Get("Str.CompareDiffTooDifferent"));
			}

			parts.AddRange(_notes.Select(note => Loc.Format(note.Key, note.Args)));
			return String.Join(" - ", parts);
		}

		/// <summary>Names the texts again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			if (_messageKey.Length > 0)
			{
				Message = Loc.Get(_messageKey);
			}
			else if (_rows.Count > 0)
			{
				Summary = BuildSummary();
			}
		}

		/// <summary>Goes to the next (or the previous) group of lines that differ, from the line that is selected; it wraps around.</summary>
		private void Go(Boolean forward)
		{
			if (_starts.Count == 0)
			{
				return;
			}

			Int32 current = _selectedRow == null ? -1 : _list.IndexOf(_selectedRow);
			Int32 target = forward
				? _starts.FirstOrDefault(start => start > current, _starts[0])
				: _starts.LastOrDefault(start => start < current, _starts[^1]);
			SelectedRow = _list[target];
		}
	}
}