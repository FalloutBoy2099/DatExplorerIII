"""Checks a translation file of the interface against the English one.

Usage: python -I Tools\\validate_language.py <code>      (for instance: sv, pl, cs)

It checks that the file is well formed XML, that it has exactly the keys of Strings.en.xaml, that no text is empty,
that every text places the same values ({0}, {1}...) as the English one, and that the patterns of the file dialog
filters (the parts after a | in "name|*.ext") are unchanged.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

X = "{http://schemas.microsoft.com/winfx/2006/xaml}Key"
folder = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "DatExplorerIII", "Resources")


def load(code):
    root = ET.parse(os.path.join(folder, "Strings.%s.xaml" % code)).getroot()
    return {element.attrib[X]: (element.text or "") for element in root}


def placeholders(text):
    return sorted(re.findall(r"\{(\d+)[^}]*\}", text))


def main():
    code = sys.argv[1]
    english = load("en")
    translation = load(code)
    problems = []

    for key in sorted(set(english) - set(translation)):
        problems.append("missing: " + key)
    for key in sorted(set(translation) - set(english)):
        problems.append("unknown: " + key)
    for key, text in translation.items():
        if not text.strip():
            problems.append("empty: " + key)
        if key in english and placeholders(text) != placeholders(english[key]):
            problems.append("placeholders differ: %s  en=%r  %s=%r" % (key, english[key], code, text))
        if key in english and "|" in english[key] and "*." in english[key]:
            en_patterns = english[key].split("|")[1::2]
            if text.split("|")[1::2] != en_patterns:
                problems.append("filter patterns differ: " + key)

    if problems:
        print("\n".join(problems))
        print("%d problem(s)" % len(problems))
        sys.exit(1)
    print("%s: OK, %d texts" % (code, len(translation)))


main()
