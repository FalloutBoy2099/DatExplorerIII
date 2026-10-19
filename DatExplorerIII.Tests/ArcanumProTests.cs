namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.ViewModels;

	/// <summary>
	/// The prototype files (.pro) of Arcanum.
	/// </summary>
	public sealed class ArcanumProTests : IDisposable
	{
		private readonly String _folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		public ArcanumProTests()
		{
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		/// <summary>
		/// A prototype with every field of its kind of object: the number of a field is the value of a 32 bit field, the arrays
		/// are empty except the field 5, which holds 7 and 8.
		/// </summary>
		private static Byte[] Build(ArcanumObjectType type, Int32 number)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);

			writer.Write(0x77);
			Byte[] prototype = new Byte[24];
			BinaryPrimitives.WriteInt16LittleEndian(prototype, -1);
			writer.Write(prototype);

			Byte[] identifier = new Byte[24];
			BinaryPrimitives.WriteInt16LittleEndian(identifier, 1);
			BinaryPrimitives.WriteInt32LittleEndian(identifier.AsSpan(8), number);
			writer.Write(identifier);
			writer.Write((Int32)type);

			Int32 bitmapSize = ArcanumFields.BitmapSize(type);
			writer.Write(new Byte[bitmapSize]);

			for (Int32 bit = 0; bit < bitmapSize * 8; bit++)
			{
				if (!ArcanumFields.TryGetWireType(type, bit, out ArcanumWireType wire))
				{
					continue;
				}

				switch (wire)
				{
					case ArcanumWireType.Int32:
						writer.Write(bit);
						break;

					case ArcanumWireType.Float:
						writer.Write(1.5f);
						break;

					case ArcanumWireType.Int64:
						writer.Write((Byte)1);
						writer.Write(777L);
						break;

					case ArcanumWireType.String:
						writer.Write((Byte)1);
						writer.Write(3);
						writer.Write(Encoding.ASCII.GetBytes("abc"));
						writer.Write((Byte)0);
						break;

					case ArcanumWireType.ObjectId:
						writer.Write((Byte)0);
						break;

					case ArcanumWireType.Int32Array when bit == 5:
						writer.Write((Byte)1);
						writer.Write(4);
						writer.Write(2);
						writer.Write(0);
						writer.Write(7);
						writer.Write(8);
						writer.Write(1);
						writer.Write(3);
						break;

					default:
						writer.Write((Byte)0);
						break;
				}
			}

			writer.Flush();
			return stream.ToArray();
		}

		private DatArchive CreateArchive(params (String Directory, String Name, Byte[] Content)[] files)
		{
			DatArchive archive = DatArchive.Create(Path.Combine(_folder, "arcanum.dat"), DatFormat.Arcanum);
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String source = Path.Combine(_folder, Guid.NewGuid().ToString("N"));
				File.WriteAllBytes(source, content);
				archive.AddFile(source, directory, name);
			}

			return archive;
		}

		[Fact]
		public void IsArcanumPro_TellsArcanumFromFallout()
		{
			Assert.True(ArcanumProReader.IsArcanumPro(Build(ArcanumObjectType.Ammo, 7058)));

			// Fallout: a big endian identifier whose first byte is the type of the object (0 to 5).
			Byte[] fallout = new Byte[200];
			fallout[3] = 0x08;
			Assert.False(ArcanumProReader.IsArcanumPro(fallout));
			fallout[0] = 0x05;
			Assert.False(ArcanumProReader.IsArcanumPro(fallout));

			Assert.False(ArcanumProReader.IsArcanumPro(Array.Empty<Byte>()));
			Assert.False(ArcanumProReader.IsArcanumPro(Build(ArcanumObjectType.Ammo, 1).AsSpan(0, 40)));
		}

		[Fact]
		public void Read_Ammo_GivesItsNumberItsKindAndItsFields()
		{
			ArcanumProto proto = ArcanumProReader.Read(Build(ArcanumObjectType.Ammo, 7058));

			Assert.Null(proto.Note);
			Assert.Equal(0x77, proto.Version);
			Assert.Equal(7058, proto.Number);
			Assert.Equal(ArcanumObjectType.Ammo, proto.Type);

			// Common and 32 bit fields, then those of the kind of object.
			Assert.Equal("4", proto.Properties.Single(property => property.Bit == 4).Value);
			Assert.Equal("1.5", proto.Properties.Single(property => property.Bit == 2).Value);
			Assert.Equal("777", proto.Properties.Single(property => property.Bit == 1).Value);
			Assert.Equal("[2] 7, 8", proto.Properties.Single(property => property.Bit == 5).Value);
			Assert.Equal("[]", proto.Properties.Single(property => property.Bit == 6).Value);
			Assert.Equal("Ammo Quantity", proto.Properties.Single(property => property.Bit == 97).Name);
			Assert.Equal("97", proto.Properties.Single(property => property.Bit == 97).Value);
			Assert.All(proto.Properties, property => Assert.False(String.IsNullOrEmpty(property.Name)));
			Assert.Equal(proto.Properties.Select(property => property.Bit).OrderBy(bit => bit), proto.Properties.Select(property => property.Bit));
		}

		[Theory]
		[InlineData(ArcanumObjectType.Wall)]
		[InlineData(ArcanumObjectType.Portal)]
		[InlineData(ArcanumObjectType.Container)]
		[InlineData(ArcanumObjectType.Scenery)]
		[InlineData(ArcanumObjectType.Projectile)]
		[InlineData(ArcanumObjectType.Weapon)]
		[InlineData(ArcanumObjectType.Ammo)]
		[InlineData(ArcanumObjectType.Armor)]
		[InlineData(ArcanumObjectType.Gold)]
		[InlineData(ArcanumObjectType.Food)]
		[InlineData(ArcanumObjectType.Scroll)]
		[InlineData(ArcanumObjectType.Key)]
		[InlineData(ArcanumObjectType.KeyRing)]
		[InlineData(ArcanumObjectType.Written)]
		[InlineData(ArcanumObjectType.Generic)]
		[InlineData(ArcanumObjectType.Pc)]
		[InlineData(ArcanumObjectType.Npc)]
		[InlineData(ArcanumObjectType.Trap)]
		public void Read_EveryKindOfObject_IsReadToTheEnd(ArcanumObjectType type)
		{
			ArcanumProto proto = ArcanumProReader.Read(Build(type, 123));

			Assert.Null(proto.Note);
			Assert.Equal(type, proto.Type);
			Assert.Equal(123, proto.Number);
			Assert.True(proto.Properties.Count > 30);
		}

		[Fact]
		public void Read_ReportsWhatCannotBeRead_WithoutFailing()
		{
			Byte[] data = Build(ArcanumObjectType.Pc, 16086);

			// Bytes after the last field.
			ArcanumProto longer = ArcanumProReader.Read(data.Concat(new Byte[] { 1, 2, 3 }).ToArray());
			Assert.NotNull(longer.Note);
			Assert.Equal(data.Length, data.Length);

			// A file that stops early: what was read is kept.
			ArcanumProto shorter = ArcanumProReader.Read(data[..(data.Length - 20)]);
			Assert.NotNull(shorter.Note);
			Assert.NotEmpty(shorter.Properties);
			Assert.True(shorter.Properties.Count < ArcanumProReader.Read(data).Properties.Count);
		}

		[Fact]
		public void Read_RefusesWhatIsNotAPrototype()
		{
			Assert.Throws<InvalidDataException>(() => ArcanumProReader.Read(new Byte[200]));

			Byte[] data = Build(ArcanumObjectType.Ammo, 1);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), 99);
			Assert.Throws<InvalidDataException>(() => ArcanumProReader.Read(data));

			// The header is there, the bitmap is not.
			Assert.Throws<InvalidDataException>(() => ArcanumProReader.Read(Build(ArcanumObjectType.Ammo, 1)[..60]));
		}

		[Fact]
		public void Read_NeverFails_WhateverTheContent()
		{
			Random random = new(2026);
			Byte[] valid = Build(ArcanumObjectType.Npc, 4000);

			for (Int32 round = 0; round < 400; round++)
			{
				Byte[] data = (Byte[])valid.Clone();
				Int32 changes = random.Next(1, 8);
				for (Int32 i = 0; i < changes; i++)
				{
					data[random.Next(56, data.Length)] = (Byte)random.Next(256);
				}

				if (round % 3 == 0)
				{
					data = data[..random.Next(0, data.Length)];
				}

				if (ArcanumProReader.IsArcanumPro(data))
				{
					try
					{
						ArcanumProReader.Read(data);
					}
					catch (InvalidDataException)
					{
					}
				}
			}

			for (Int32 length = 0; length < valid.Length; length++)
			{
				Byte[] data = valid[..length];
				if (ArcanumProReader.IsArcanumPro(data))
				{
					try
					{
						ArcanumProReader.Read(data);
					}
					catch (InvalidDataException)
					{
					}
				}
			}
		}

		[Fact]
		public async Task Viewer_ShowsAnArcanumPrototype()
		{
			Byte[] ammo = Build(ArcanumObjectType.Ammo, 7058);
			Byte[] broken = ammo[..(ammo.Length - 10)];
			using DatArchive archive = CreateArchive(("proto", "07058 - Ammo.pro", ammo), ("proto", "broken.pro", broken), ("data", "a.txt", Encoding.ASCII.GetBytes("text")));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("proto\\07058 - Ammo.pro")!);
			Assert.True(viewer.IsPrototype);
			Assert.Equal(2, viewer.Prototype.Sections.Count);
			Assert.Equal(3, viewer.Prototype.Sections[0].Rows.Count);
			Assert.Contains("7058", viewer.Prototype.Sections[0].Rows[1].Value);
			Assert.Contains(viewer.Prototype.Sections[1].Rows, row => row.Label == "Ammo Quantity" && row.Value == "97");

			// The fields that hold nothing are left out.
			Assert.DoesNotContain(viewer.Prototype.Sections[1].Rows, row => row.Value == "[]");

			// A file that stops early is shown with what it holds, and says so.
			await viewer.LoadAsync(archive, archive.Find("proto\\broken.pro")!);
			Assert.True(viewer.IsPrototype);
			Assert.Equal(4, viewer.Prototype.Sections[0].Rows.Count);

			await viewer.LoadAsync(archive, archive.Find("data\\a.txt")!);
			Assert.False(viewer.IsPrototype);
			Assert.Empty(viewer.Prototype.Sections);
		}

	}
}