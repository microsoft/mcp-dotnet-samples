import os

from pypandoc_hwpx.PandocToHwpx import PandocToHwpx


def convert_to_hwpx(input_path: str, output_path: str, reference_hwpx: str) -> str:
    """Convert a document file (.docx, .html, or .md) to .hwpx format."""
    PandocToHwpx.convert_to_hwpx(input_path, output_path, reference_hwpx)
    return os.path.abspath(output_path)


def get_default_reference() -> str:
    """Get the default blank.hwpx reference file path bundled with pypandoc-hwpx."""
    import pypandoc_hwpx

    pkg_dir = os.path.dirname(os.path.abspath(pypandoc_hwpx.__file__))
    blank = os.path.join(pkg_dir, "blank.hwpx")
    if os.path.isfile(blank):
        return blank
    return ""
