"""Regression fixture for 129 Artist View Road sample PDF.
Run: python -m pip install pymupdf && python tools/validate_sample_import.py /path/to/estimate.pdf
No source PDF or customer information is committed to GitHub.
"""
import re
import sys
import fitz

LINE = re.compile(r"(?m)^\s*(\d{1,4})\.\s+(.+?)\s+(\d[\d,]*\.\d{2})\s+(SF|LF|SY|EA|HR|DA|CY|CF|SQ)\b")
ROOMS = {2: "General", 3: "Bathroom", 4: "Bathroom", 5: "Laundry Room", 6: "Laundry Room"}
EXPECTED = {"General": 13, "Bathroom": 34, "Laundry Room": 24}

def parse(path):
    result = []
    with fitz.open(path) as pdf:
        for page_num in range(2, 7):
            text = pdf[page_num - 1].get_text(sort=True)
            for match in LINE.finditer(text):
                result.append({"number": int(match[1]), "description": match[2].strip(),
                               "quantity": float(match[3].replace(",", "")), "unit": match[4],
                               "room": ROOMS[page_num], "page": page_num})
    return result

def validate(items):
    assert len(items) == 71, f"Expected 71 items; got {len(items)}"
    assert [item["number"] for item in items] == list(range(1, 72)), "Missing or duplicate item numbers"
    for room, expected in EXPECTED.items():
        actual = sum(item["room"] == room for item in items)
        assert actual == expected, f"{room}: expected {expected}, got {actual}"
    quantities = {item["number"]: (item["quantity"], item["unit"]) for item in items}
    for number, value in {38:(38.72,"SF"), 55:(69.17,"LF"), 61:(69.17,"LF"),
                          62:(69.17,"LF"), 63:(185.89,"SF"), 64:(185.89,"SF")}.items():
        assert quantities[number] == value, f"Line {number}: expected {value}, got {quantities[number]}"
    print("PASS: 71 line items; room counts and key reconstruction quantities verified.")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python tools/validate_sample_import.py <sample-estimate.pdf>")
    validate(parse(sys.argv[1]))
