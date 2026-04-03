import os
import glob
import importlib

# Sucht alle Dateien im aktuellen Verzeichnis, die dem Muster t??.py entsprechen
_path = os.path.dirname(__file__)
_files = glob.glob(os.path.join(_path, "t??.py"))

for _f in _files:
    _module_name = os.path.basename(_f)[:-3] # Entfernt die Endung .py
    # Importiert das Modul relativ zu diesem Paket
    importlib.import_module("." + _module_name, __package__)