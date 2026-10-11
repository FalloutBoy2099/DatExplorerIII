# DatExplorerIII (by Kain)

DAT archive explorer for Fallout 1, Fallout 2 and Arcanum: browse, extract, import, rename, delete and repack files.

A rewrite in **.NET 10 / WPF (Fluent theme)** of *DAT Explorer II* by Mr.STALIN, which itself replaced DatExpl 1.43 by Dims.

![DatExplorerIII: the folder tree, the files of a folder and the preview of a .frm picture](_screenshots/datex3_1.jpg)

## Features

- **Comparison of two DATs** (*Tools* menu, files and/or folders): state of each file (identical, different, only on the left or on the right), filters, a preview of the two versions side by side, copy or move of the selected files to the left or to the right, then saving of each side. What the comparison ignores is set in *Tools > Settings > Comparison*: white space characters, blank lines, case, line endings (Windows/Unix/Mac), code page, numbers, comments, a missing line break at the end of the file; *text content* method (text files are compared line by line with these tolerances) or *binary content* method (byte for byte); synchronized scrolling of the two versions of a file. The folder tree, the file list (with a drop-down list to choose the folder) and the two DATs are arranged side by side or one above the other, as you like; the tool bar only has icons.

  ![Comparison of two DATs: the state of each file and the two versions of a text side by side](_screenshots/datex3_4.JPG)
- **Folder opened as a DAT** (*File > Open a folder as a DAT…*, or a folder path on the command line): a folder that holds the files of a DAT (an unpacked `master.dat`, for instance) can be browsed, edited and viewed like an archive (`[DIR]` in the tree). **Save as…** turns it into a real `.dat` file (Fallout 2, Fallout 1 or Arcanum, as you choose) without touching the folder; it also works for any archive, to write a copy of it under another name.
- **File association** (*Help* menu): "Associate .dat files" and, separately, "Associate .patch files" for the `.patch0` to `.patch9` files of Arcanum; for the current user, without administrator rights.
- **Lower-casing** of a folder, its sub folders and their files (context menu of the tree, *Edit* menu); the whole archive from the root.
- **Settings** (*Tools > Settings…* menu, window with tabs): *General* (single instance, window border, separators between the blocks, memory of the size and of the position of the window) and *Comparison* (see above).
- **Syntax coloring** of the `.bio` biographies (lines past the 22nd and characters past the 26th of a line, which the box of the game does not show) and of the `.lst` lists (comments after a semicolon).
- **Scroll bars** with a width and a smallest slider that you can set (*Tools > Settings > General*): no more tiny slider in a list of thousands of files.
- **Single instance** (*Tools > Settings* menu, on by default): a file opened while the program is already running (double click on a `.dat`, command line) opens in the existing window; when it is off, every launch opens a new window.
- Several archives open at the same time (Fallout 1, Fallout 2 and Arcanum, the format being detected from the content; the `.dat` and Arcanum `.patch0` to `.patch9` extensions are accepted), through the menu, by dropping them on the tree or on the command line (`DatExplorerIII.exe file.dat`).
- File sizes that people can read (B, KB, MB, GB, TB in English; o, Ko, Mo, Go, To in French; Б/КБ/МБ/ГБ/ТБ in Russian...) in the list and in the viewer.
- List of the last 10 DAT files opened successfully in the *File* menu (a file that no longer exists is removed from it), with an entry to clear the list.
- Association of the `.dat` files with the program (for the current user, without administrator rights).
- Navigation by tree, three display modes (details, list, large icons), sorting by column.
- Search in the current folder (`Enter` / `Shift+Enter`) and in the whole DAT (list of results).
- Extraction of files, folders or the whole DAT with their tree, and drag and drop to Windows Explorer.
- Import of files and folders (menu or drag and drop from Explorer), creation, renaming and deletion of files and folders.
- Creation of new Fallout 1, Fallout 2 or Arcanum DATs.
- **Arcanum DATs** (reading and writing): files compressed with zlib, empty folders kept, identifier (GUID) of the module kept when saving. The quick save only exists for Fallout 2.
- Full save, or quick save for Fallout 2 (adds the files and rewrites the tree without rebuilding the archive: not recommended, the space of the deleted files is not recovered and an interruption damages the archive).
- **Tolerant reading and repair**: a damaged archive (extra bytes after the end, cut list of files) is offered in recovery mode instead of being refused. The *Tools* menu can verify the integrity (reading of all the files), list the problems and remove the damaged files. Saving a repaired archive writes a new file and keeps the original as a `.bak`.
- **File viewer** chosen by the extension, in the preview pane of the main window (on by default; *View* menu or `Ctrl+P`) or in a separate window (`F3`, *Edit* menu, or double click depending on the *Double click opens* setting):
  - texts (`.lst`, `.txt`, `.msg`, `.bio` (biographies of the premade characters), `.cfg`, `.ini`, `.ssl`...) with detection of the encoding (BOM, UTF-8, UTF-16, Windows-1252 for Western Europe, Windows-1250 for Polish, Windows-1251 for Russian) and a manual choice of the encoding;
  - `.frm` images (animations, 6 directions, the palette with the same name as the picture if there is one (`hero.pal` for `hero.frm`), else the `color.pal` of the DAT, else the palette of the game built into the program (the same for all the `.frm` of Fallout 1 and 2), palette of your choice, zoom), `.rix` and `.pal` / `.act` palettes;
  - `.map` maps of Fallout 1 and 2: header, starting point, script of the map, variables, tiles used, objects by level and by type, scripts with the name of the programs, exits to the other maps (the prototypes of the game are needed to read the objects: they are looked for in the archive of the map then in the other open archives);
  - **scripts**: the compiled `.int` scripts of Fallout 1 and 2 are **decompiled** and shown as `.ssl` source (names of the procedures and of the shared variables, strings, conditions, `while` and `for` loops, `break` and `continue`; the comments, the macros and the names of the local variables are not in the compiled file), with the functions of the game and of sfall recognized; the `.ssl` scripts and the `.h` headers are shown with **syntax coloring** (words of the language, functions of the game, numbers, texts, comments, preprocessor lines `#define` / `#include`), the same as for a decompiled `.int`;
  - `.mve` movies (Interplay MVE: picture decoded on the fly, DPCM sound, play, pause and a position bar to go anywhere), with their **subtitles**: the `.sve` file with the same name (in the language of the interface if the archive has several, else English) is shown over the picture, synchronized with the pictures of the movie (can be turned off in *Tools > Settings > General*);

    ![An .mve movie with its subtitles shown over the picture](_screenshots/datex3_3.jpg)
  - `.aaf` fonts (Fallout 1 and 2, nine levels of gray) and `.fon` fonts (Fallout 1, one bit per pixel), drawn as a table of their characters with their measures;
  - `.lip` lip synchronization: version, sound file, phonemes (code, sound in the phonetic alphabet, picture of the talking head) and markers;
  - `.gam` variables (game and maps) and `.sve` subtitles as tables: number of the variable (the one the scripts use), name, initial value and comment; picture number and text for the subtitles;
  - `.pro` prototypes of objects (objects, creatures, scenery, walls, tiles, miscellaneous: armors, weapons, drugs, ammunition, characteristics of the creatures, doors, stairs...);
  - `.gcd` character sheets (statistics, resistances, tagged skills and traits, names translated into the 11 languages);
  - `.pro` prototypes of Arcanum (detected automatically: an Arcanum `.pro` starts with `77 00 00 00` and the integer −1, a Fallout one with an identifier whose first byte is 0 to 5): type of object, number and fields with their technical names (verified on real files for the ammunition and the characters);
  - Arcanum files: `.art` images (rotations, pictures, up to four palettes), tables for the `.mes` messages and the `.dlg` dialogs (sortable, with the detected encoding);
  - common `.png`, `.bmp`, `.jpg`, `.gif` images and `.mp3` sounds (NLayer decoder, MIT license: no Windows codec needed);
  - `.acm` sounds (built-in decoder, playback, waveform, mono/stereo detected from the sound because the header is wrong for speech and sound effects, with a manual choice) and `.wav`.

    ![An .acm sound: waveform, play button and choice of the channels](_screenshots/datex3_2.jpg)
- Custom title bar with two buttons: theme (automatic, light or dark) and language of the interface. The choice is remembered and applied at once, dialog boxes included.
- Interface in English, French, Russian, Spanish, German, Italian, Portuguese, Swedish, Polish, Czech and Simplified Chinese (the language of Windows by default, English otherwise). All the texts are translated, error messages and keyboard shortcuts included.
- The position of the window is remembered.

## Limits

- The compression of added files is not supported for the Fallout 1 format (they are stored as they are). The existing compressed (LZSS) files are read and kept.
- The repair cannot rebuild what has disappeared: an archive whose end is lost (missing footer) cannot be recovered, and a damaged file is removed, not fixed.
- A Fallout DAT does not remember empty folders: they disappear when it is closed (those of Arcanum keep them).
- Drag and drop to Explorer first extracts the selection to a temporary folder (limited to 512 MB; use "Extract" beyond that).

## Build

Requirements: .NET 10 SDK and Windows.

```
dotnet build DatExplorerIII.sln
dotnet test DatExplorerIII.sln
dotnet run --project DatExplorerIII
```

`.\Publish.ps1` creates two single-file executables in `publish\`: `DatExplorerIII.exe` (under 1 MB, needs the .NET 10 Desktop runtime) and `DatExplorerIII-NET.exe` (about 62 MB, the .NET runtime is included: nothing to install).

## Layout

| Folder | Content |
| --- | --- |
| `DatExplorerIII/Models` | Data and description objects: archive file (`DatEntry`), problems, results, formats, enumerations, settings, languages, dialog buttons... |
| `DatExplorerIII/Formats` | Readers of the Fallout and Arcanum formats (`.art`, `.mes`, `.dlg`, `.mp3`): `.frm` and `.rix` images, `.pal` and `.act` palettes, `.mve` movies, `.gcd` characters, `.pro` prototypes, `.bio` biographies, `.acm` sounds (port of libacm, ISC license) and `.wav`. |
| `DatExplorerIII/Core` | Reading (strict or tolerant), verification, repair, editing and writing of the DAT archives. |
| `DatExplorerIII/ViewModels`, `Views`, `Services`, `Infrastructure` | WPF interface (MVVM without any external library). |
| `DatExplorerIII/Resources` | Icons and texts (`Strings.<language>.xaml`). |
| `DatExplorerIII.Tests` | xUnit tests: format readers and text decoding (with the real files of the game if `DATEXPLORERIII_FALLOUT_DIR` names the Fallout 2 folder), core (Fallout 1 and 2 round trip, quick save, LZSS, safety of the paths), damaged archives (recovery, verification, fuzzing), logic of the interface and completeness of the translations. |

The code style follows the `.editorconfig`: block scoped namespaces, `using` inside the namespace, CLR types (`String`, `Int32`, `Boolean`...), names in English.
