namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Specialized;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Media;

	/// <summary>
	/// Lays items of the same size out in rows, from the left to the right then from the top to the bottom, like a
	/// <see cref="WrapPanel"/>, but makes only the items that can be seen: a folder of thousands of files shows as fast as a
	/// folder of a few. It scrolls vertically; the size of an item is the size of the first one.
	/// </summary>
	public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
	{
		/// <summary>The rows made above and below the ones that can be seen, so that a short scroll shows items that are ready.</summary>
		private const Int32 CachedRows = 1;

		/// <summary>The distance of a line of the scroll bar or of a notch of the wheel, in device independent pixels.</summary>
		private const Double LineDistance = 16;

		private Size _itemSize = Size.Empty;
		private Int32 _perRow = 1;
		private Size _extent;
		private Size _viewport;
		private Point _offset;

		public Boolean CanHorizontallyScroll { get; set; }

		public Boolean CanVerticallyScroll { get; set; }

		public Double ExtentWidth => _extent.Width;

		public Double ExtentHeight => _extent.Height;

		public Double ViewportWidth => _viewport.Width;

		public Double ViewportHeight => _viewport.Height;

		public Double HorizontalOffset => 0;

		public Double VerticalOffset => _offset.Y;

		public ScrollViewer? ScrollOwner { get; set; }

		protected override Size MeasureOverride(Size availableSize)
		{
			// The children must be asked for before the generator is used (a rule of the panels that virtualize).
			UIElementCollection children = InternalChildren;
			IItemContainerGenerator generator = ItemContainerGenerator;
			Int32 count = ItemsControl.GetItemsOwner(this)?.Items.Count ?? 0;

			if (count == 0)
			{
				RemoveAll(generator, children);
				UpdateScrollInfo(availableSize, new Size(0, 0));
				return new Size(Double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 0);
			}

			if (_itemSize.IsEmpty)
			{
				_itemSize = MeasureFirstItem(generator, children);
			}

			Double width = Double.IsInfinity(availableSize.Width) ? _itemSize.Width * count : availableSize.Width;
			_perRow = Math.Max(1, (Int32)(width / _itemSize.Width));
			Int32 rows = (count + _perRow - 1) / _perRow;
			UpdateScrollInfo(availableSize, new Size(width, rows * _itemSize.Height));

			// Without a limit of height (no scroll viewer), every item is made.
			Double viewportHeight = Double.IsInfinity(availableSize.Height) ? _extent.Height : availableSize.Height;
			Int32 firstRow = Math.Max(0, (Int32)(_offset.Y / _itemSize.Height) - CachedRows);
			Int32 lastRow = Math.Min(rows - 1, (Int32)Math.Ceiling((_offset.Y + viewportHeight) / _itemSize.Height) + CachedRows);
			Int32 first = firstRow * _perRow;
			Int32 last = Math.Min(count - 1, ((lastRow + 1) * _perRow) - 1);

			RemoveOutside(generator, children, first, last);
			Realize(generator, children, first, last);

			return new Size(width, Double.IsInfinity(availableSize.Height) ? _extent.Height : availableSize.Height);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			IItemContainerGenerator generator = ItemContainerGenerator;
			for (Int32 index = 0; index < InternalChildren.Count; index++)
			{
				Int32 item = generator.IndexFromGeneratorPosition(new GeneratorPosition(index, 0));
				InternalChildren[index].Arrange(ItemRect(item));
			}

			return finalSize;
		}

		protected override void OnItemsChanged(Object sender, ItemsChangedEventArgs args)
		{
			switch (args.Action)
			{
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Move:
					RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
					break;

				case NotifyCollectionChangedAction.Reset:
					// Another list (another folder): it starts at its top, and its items may have another size.
					_itemSize = Size.Empty;
					_offset = new Point();
					ScrollOwner?.InvalidateScrollInfo();
					break;
			}

			base.OnItemsChanged(sender, args);
		}

		/// <summary>Scrolls so that an item can be seen (the keyboard, or a file that is shown in the list).</summary>
		protected override void BringIndexIntoView(Int32 index)
		{
			if (_itemSize.IsEmpty || index < 0)
			{
				return;
			}

			ScrollToShow(Rect.Offset(ItemRect(index), 0, _offset.Y));
		}

		public Rect MakeVisible(Visual visual, Rect rectangle)
		{
			if (visual is UIElement element && InternalChildren.IndexOf(element) is Int32 child and >= 0)
			{
				Int32 item = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(child, 0));
				ScrollToShow(Rect.Offset(ItemRect(item), 0, _offset.Y));
			}

			return rectangle;
		}

		public void LineUp() => SetVerticalOffset(_offset.Y - LineDistance);

		public void LineDown() => SetVerticalOffset(_offset.Y + LineDistance);

		public void PageUp() => SetVerticalOffset(_offset.Y - _viewport.Height);

		public void PageDown() => SetVerticalOffset(_offset.Y + _viewport.Height);

		public void MouseWheelUp() => SetVerticalOffset(_offset.Y - (LineDistance * SystemParameters.WheelScrollLines));

		public void MouseWheelDown() => SetVerticalOffset(_offset.Y + (LineDistance * SystemParameters.WheelScrollLines));

		public void LineLeft()
		{
		}

		public void LineRight()
		{
		}

		public void PageLeft()
		{
		}

		public void PageRight()
		{
		}

		public void MouseWheelLeft()
		{
		}

		public void MouseWheelRight()
		{
		}

		public void SetHorizontalOffset(Double offset)
		{
		}

		public void SetVerticalOffset(Double offset)
		{
			Double clamped = Math.Max(0, Math.Min(offset, _extent.Height - _viewport.Height));
			if (Double.IsNaN(clamped) || clamped == _offset.Y)
			{
				return;
			}

			_offset.Y = clamped;
			ScrollOwner?.InvalidateScrollInfo();
			InvalidateMeasure();
		}

		/// <summary>Where an item is in the panel, the scroll taken into account.</summary>
		private Rect ItemRect(Int32 item)
		{
			Int32 row = item / _perRow;
			Int32 column = item % _perRow;
			return new Rect(column * _itemSize.Width, (row * _itemSize.Height) - _offset.Y, _itemSize.Width, _itemSize.Height);
		}

		/// <summary>Scrolls just enough for a place of the panel (without the scroll) to be seen.</summary>
		private void ScrollToShow(Rect place)
		{
			if (place.Top < _offset.Y)
			{
				SetVerticalOffset(place.Top);
			}
			else if (place.Bottom > _offset.Y + _viewport.Height)
			{
				SetVerticalOffset(place.Bottom - _viewport.Height);
			}
		}

		private void UpdateScrollInfo(Size available, Size extent)
		{
			Size viewport = new(Double.IsInfinity(available.Width) ? extent.Width : available.Width, Double.IsInfinity(available.Height) ? extent.Height : available.Height);
			Double maxOffset = Math.Max(0, extent.Height - viewport.Height);
			Point offset = new(0, Math.Min(_offset.Y, maxOffset));
			if (extent != _extent || viewport != _viewport || offset != _offset)
			{
				_extent = extent;
				_viewport = viewport;
				_offset = offset;
				ScrollOwner?.InvalidateScrollInfo();
			}
		}

		/// <summary>The size of the first item, which all the items have.</summary>
		private Size MeasureFirstItem(IItemContainerGenerator generator, UIElementCollection children)
		{
			Realize(generator, children, 0, 0);
			Size size = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator.ContainerFromIndex(0) is UIElement first ? first.DesiredSize : new Size(1, 1);
			return new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
		}

		/// <summary>Makes the items of a range that are not made yet, in the order of the children, and measures them.</summary>
		private void Realize(IItemContainerGenerator generator, UIElementCollection children, Int32 first, Int32 last)
		{
			GeneratorPosition start = generator.GeneratorPositionFromIndex(first);
			Int32 child = start.Offset == 0 ? start.Index : start.Index + 1;
			using (generator.StartAt(start, GeneratorDirection.Forward, true))
			{
				for (Int32 item = first; item <= last; item++, child++)
				{
					UIElement container = (UIElement)generator.GenerateNext(out Boolean isNew);
					if (isNew)
					{
						if (child >= children.Count)
						{
							AddInternalChild(container);
						}
						else
						{
							InsertInternalChild(child, container);
						}

						generator.PrepareItemContainer(container);
					}

					container.Measure(_itemSize.IsEmpty ? new Size(Double.PositiveInfinity, Double.PositiveInfinity) : _itemSize);
				}
			}
		}

		/// <summary>Removes the items that are out of a range (they scrolled away).</summary>
		private void RemoveOutside(IItemContainerGenerator generator, UIElementCollection children, Int32 first, Int32 last)
		{
			for (Int32 child = children.Count - 1; child >= 0; child--)
			{
				GeneratorPosition position = new(child, 0);
				Int32 item = generator.IndexFromGeneratorPosition(position);
				if (item < first || item > last)
				{
					generator.Remove(position, 1);
					RemoveInternalChildRange(child, 1);
				}
			}
		}

		private void RemoveAll(IItemContainerGenerator generator, UIElementCollection children)
		{
			if (children.Count > 0)
			{
				generator.RemoveAll();
				RemoveInternalChildRange(0, children.Count);
			}
		}
	}
}