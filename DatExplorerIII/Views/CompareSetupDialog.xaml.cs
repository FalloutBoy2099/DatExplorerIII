namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using Microsoft.Win32;
	using Models.Comparison;
	using Services.Interface;

	/// <summary>
	/// Asks which two archives to compare: for each side a DAT file or a folder (a folder is opened like a DAT). Each side has a
	/// box that proposes the archives that are open and the ones used lately, buttons to browse, and accepts a file or a folder
	/// dropped on it; a line tells what the side is.
	/// </summary>
	public partial class CompareSetupDialog : Window
	{
		public CompareSetupDialog(String title, String? left, String? right, IReadOnlyList<String> suggestions)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Title = title;
			foreach (ComboBox box in new[] { LeftBox, RightBox })
			{
				box.ItemsSource = suggestions;
				box.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
			}

			LeftBox.Text = left ?? String.Empty;
			RightBox.Text = right ?? String.Empty;
			Validate();

			Loaded += (_, _) => (String.IsNullOrWhiteSpace(LeftBox.Text) ? LeftBox : RightBox).Focus();
		}

		public ComparisonSources Sources => new(LeftBox.Text.Trim(), RightBox.Text.Trim());

		private static Boolean Exists(String path) => File.Exists(path) || Directory.Exists(path);

		/// <summary>What a source is, in a few words: a folder, or a DAT file with its size and date.</summary>
		internal static String Describe(String path)
		{
			if (path.Length == 0)
				return String.Empty;

			if (Directory.Exists(path))
				return Loc.Get("Str.CompareKindFolder");

			if (File.Exists(path))
			{
				FileInfo info = new(path);
				return Loc.Format("Str.CompareKindFile", SizeFormatter.Format(info.Length), info.LastWriteTime.ToString("d", CultureInfo.CurrentCulture));
			}

			return String.Empty;
		}

		private void OnTextChanged(Object sender, TextChangedEventArgs e)
		{
			Validate();
		}

		/// <summary>The two sources must exist and be two different things; the button of the dialog waits for that.</summary>
		private void Validate()
		{
			if (ErrorText == null || OkButton == null || LeftInfo == null || RightInfo == null)
				return;

			ComparisonSources sources = Sources;
			LeftInfo.Text = Describe(sources.Left);
			RightInfo.Text = Describe(sources.Right);

			String error = String.Empty;
			Boolean ready = sources.Left.Length > 0 && sources.Right.Length > 0;

			if (ready)
			{
				foreach (String path in new[] { sources.Left, sources.Right })
				{
					if (!Exists(path))
					{
						error = Loc.Format("Str.CompareErrorMissing", path);
						ready = false;
						break;
					}
				}

				if (ready && IsSame(sources.Left, sources.Right))
				{
					error = Loc.Get("Str.CompareErrorSame");
					ready = false;
				}
			}

			ErrorText.Text = error;
			OkButton.IsEnabled = ready;
		}

		private static Boolean IsSame(String left, String right)
		{
			try
			{
				return String.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
			}
			catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return false;
			}
		}

		private void ChooseFile(ComboBox box)
		{
			OpenFileDialog dialog = new()
			{
				Title = Loc.Get("Str.CompareChooseFile"),
				Filter = Loc.Get("Str.DatFilter"),
			};

			if (dialog.ShowDialog(this) == true)
				box.Text = dialog.FileName;
		}

		private void ChooseFolder(ComboBox box)
		{
			OpenFolderDialog dialog = new() { Title = Loc.Get("Str.CompareChooseFolder") };
			if (dialog.ShowDialog(this) == true)
				box.Text = dialog.FolderName;
		}

		private void OnLeftFile(Object sender, RoutedEventArgs e) => ChooseFile(LeftBox);

		private void OnLeftFolder(Object sender, RoutedEventArgs e) => ChooseFolder(LeftBox);

		private void OnRightFile(Object sender, RoutedEventArgs e) => ChooseFile(RightBox);

		private void OnRightFolder(Object sender, RoutedEventArgs e) => ChooseFolder(RightBox);

		/// <summary>The two sides change places.</summary>
		private void OnSwap(Object sender, RoutedEventArgs e)
		{
			(LeftBox.Text, RightBox.Text) = (RightBox.Text, LeftBox.Text);
		}

		/// <summary>A file or a folder of the desktop or of Explorer can be dropped on a side.</summary>
		private void OnPreviewDragOver(Object sender, DragEventArgs e)
		{
			e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		}

		private void OnDropLeft(Object sender, DragEventArgs e) => DropPath(LeftBox, e);

		private void OnDropRight(Object sender, DragEventArgs e) => DropPath(RightBox, e);

		private static void DropPath(ComboBox box, DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is String[] { Length: > 0 } paths)
				box.Text = paths[0];

			e.Handled = true;
		}

		private void OnOk(Object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}