# -*- coding: utf-8 -*-
import math
import xml.sax.saxutils as saxutils

def esc(text):
    if text is None:
        return ""
    return saxutils.escape(str(text), {'"': "&quot;", "'": "&apos;"})

def format_compartment_html(items, is_method=False, width=260):
    if not items:
        return "", 18
    
    # Calculate characters that fit per line (roughly 7.0 pixels per character for 11px font)
    chars_per_line = max(30, int((width - 24) / 7.0))
    
    formatted_items = []
    total_lines = 0
    for item in items:
        # Escape &, <, >, " so draw.io HTML engine decodes and displays them cleanly
        safe_item = item.replace('&', '&amp;').replace('<', '&amp;lt;').replace('>', '&amp;gt;').replace('"', '&quot;')
        formatted_items.append(safe_item)
        
        # Estimate how many lines this item will wrap to
        item_len = len(item)
        lines_count = max(1, math.ceil(item_len / chars_per_line))
        total_lines += lines_count
    
    # Separate items by HTML line break <br/> (which must be &lt;br/&gt; in XML attribute)
    val = "&lt;br/&gt;".join(formatted_items)
    
    line_h = 20 if is_method else 18
    calc_h = max(24, total_lines * line_h + 10)
    return val, calc_h

class ClassDiagramBuilder:
    def __init__(self, title, page_width=1400, page_height=1200):
        self.title = title
        self.page_width = page_width
        self.page_height = page_height
        self.id_counter = 1
        self.classes = []
        self.edges = []

    def get_id(self):
        val = f"c_{self.id_counter}"
        self.id_counter += 1
        return val

    def add_class(self, class_id, name, attributes=None, methods=None, x=100, y=100, width=260, is_interface=False):
        if attributes is None:
            attributes = []
        if methods is None:
            methods = []
            
        attr_val, attr_h = format_compartment_html(attributes, is_method=False, width=width)
        meth_val, meth_h = format_compartment_html(methods, is_method=True, width=width)
        
        header_h = 38 if is_interface else 28
        total_h = header_h + attr_h + 8 + meth_h
        
        # Use proper «interface» guillemets so draw.io does not confuse with HTML tag
        header_val = f"&amp;laquo;interface&amp;raquo;&lt;br/&gt;&lt;b&gt;{name}&lt;/b&gt;" if is_interface else f"&lt;b&gt;{name}&lt;/b&gt;"
        
        c = {
            "id": class_id,
            "name": name,
            "header_val": header_val,
            "attr_val": attr_val,
            "meth_val": meth_val,
            "x": x,
            "y": y,
            "width": width,
            "height": total_h,
            "header_h": header_h,
            "attr_h": attr_h,
            "meth_h": meth_h,
            "attr_id": self.get_id(),
            "sep_id": self.get_id(),
            "meth_id": self.get_id()
        }
        self.classes.append(c)
        return c

    def add_inheritance(self, source_id, target_id, label="Inheritance"):
        edge_id = self.get_id()
        self.edges.append({
            "id": edge_id,
            "type": "inheritance",
            "source": source_id,
            "target": target_id,
            "label": label,
            "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=0;endSize=12;strokeColor=#000000;fontSize=11;labelBackgroundColor=#ffffff;"
        })

    def add_realization(self, source_id, target_id, label="Implements"):
        edge_id = self.get_id()
        self.edges.append({
            "id": edge_id,
            "type": "realization",
            "source": source_id,
            "target": target_id,
            "label": label,
            "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=block;endFill=0;endSize=12;strokeColor=#000000;fontSize=11;labelBackgroundColor=#ffffff;"
        })

    def add_dependency(self, source_id, target_id, label="Dependency"):
        edge_id = self.get_id()
        self.edges.append({
            "id": edge_id,
            "type": "dependency",
            "source": source_id,
            "target": target_id,
            "label": label,
            "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=open;endSize=8;strokeColor=#000000;fontSize=11;labelBackgroundColor=#ffffff;"
        })

    def add_association(self, source_id, target_id, label="Association"):
        edge_id = self.get_id()
        self.edges.append({
            "id": edge_id,
            "type": "association",
            "source": source_id,
            "target": target_id,
            "label": label,
            "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=open;endSize=8;strokeColor=#000000;fontSize=11;labelBackgroundColor=#ffffff;"
        })

    def build_xml(self):
        # Determine max Y and max X to adjust page size dynamically
        max_x = max([c['x'] + c['width'] for c in self.classes] + [self.page_width]) + 100
        max_y = max([c['y'] + c['height'] for c in self.classes] + [self.page_height]) + 100
        
        xml = f'''<mxGraphModel dx="1277" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{max_x}" pageHeight="{max_y}" math="0" shadow="0">
  <root>
    <mxCell id="0" />
    <mxCell id="1" parent="0" />
'''
        # Add classes (3-compartment swimlane)
        for c in self.classes:
            xml += f'''    <mxCell id="{c['id']}" value="{c['header_val']}" style="swimlane;fontStyle=0;align=center;verticalAlign=top;childLayout=stackLayout;horizontal=1;startSize={c['header_h']};horizontalStack=0;resizeParent=1;resizeParentMax=0;resizeLast=0;collapsible=0;marginBottom=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#000000;" vertex="1" parent="1">
      <mxGeometry x="{c['x']}" y="{c['y']}" width="{c['width']}" height="{c['height']}" as="geometry" />
    </mxCell>
    <mxCell id="{c['attr_id']}" value="{c['attr_val']}" style="text;strokeColor=none;fillColor=none;align=left;verticalAlign=top;spacingLeft=8;spacingRight=6;spacingTop=4;spacingBottom=4;overflow=hidden;rotatable=0;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;whiteSpace=wrap;html=1;fontSize=11;" vertex="1" parent="{c['id']}">
      <mxGeometry y="{c['header_h']}" width="{c['width']}" height="{c['attr_h']}" as="geometry" />
    </mxCell>
    <mxCell id="{c['sep_id']}" value="" style="line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;spacingLeft=3;spacingRight=3;rotatable=0;labelPosition=right;points=[];portConstraint=eastwest;strokeColor=inherit;" vertex="1" parent="{c['id']}">
      <mxGeometry y="{c['header_h'] + c['attr_h']}" width="{c['width']}" height="8" as="geometry" />
    </mxCell>
    <mxCell id="{c['meth_id']}" value="{c['meth_val']}" style="text;strokeColor=none;fillColor=none;align=left;verticalAlign=top;spacingLeft=8;spacingRight=6;spacingTop=4;spacingBottom=4;overflow=hidden;rotatable=0;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;whiteSpace=wrap;html=1;fontSize=11;" vertex="1" parent="{c['id']}">
      <mxGeometry y="{c['header_h'] + c['attr_h'] + 8}" width="{c['width']}" height="{c['meth_h']}" as="geometry" />
    </mxCell>
'''

        # Add edges
        for e in self.edges:
            lbl = esc(e.get("label", ""))
            xml += f'''    <mxCell id="{e['id']}" value="{lbl}" style="{e['style']}" edge="1" parent="1" source="{e['source']}" target="{e['target']}">
      <mxGeometry relative="1" as="geometry" />
    </mxCell>
'''
        xml += '''  </root>
</mxGraphModel>'''
        return xml
