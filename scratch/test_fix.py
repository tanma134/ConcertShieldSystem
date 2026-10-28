# -*- coding: utf-8 -*-
import math
import xml.sax.saxutils as saxutils

def format_html_cell(lines, is_method=False, max_char_per_line=60):
    if not lines:
        return "", 18
    
    # Each item on its own line using &lt;br/&gt;
    # Any '<' or '>' in generic types like List<T> must be &amp;lt; and &amp;gt;
    formatted_items = []
    total_lines = 0
    for line in lines:
        # replace & first, then < and >, and quotes
        safe_line = line.replace('&', '&amp;').replace('<', '&amp;lt;').replace('>', '&amp;gt;').replace('"', '&quot;')
        formatted_items.append(safe_line)
        # Estimate wrap lines
        wrap_count = max(1, math.ceil(len(line) / max_char_per_line))
        total_lines += wrap_count
        
    val = "&lt;br/&gt;".join(formatted_items)
    line_h = 20 if not is_method else 22
    calc_h = max(24, total_lines * line_h + 8)
    return val, calc_h

print("Test helper defined.")
