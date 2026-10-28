# -*- coding: utf-8 -*-
import xml.sax.saxutils as saxutils

def esc(text):
    if text is None:
        return ""
    return saxutils.escape(str(text), {'"': "&quot;", "'": "&apos;"})

class SequenceDiagramBuilder:
    def __init__(self, title, lifelines, page_width=3200, page_height=2400, spacing=360):
        self.title = title
        self.spacing = spacing
        
        # Ensure generous 360px spacing between lifelines so text never overlaps
        for idx, l in enumerate(lifelines):
            l["cx"] = 100 + idx * spacing
        self.lifelines = lifelines
        
        required_w = 100 + len(lifelines) * spacing + 250
        self.page_width = max(page_width, required_w)
        self.page_height = page_height
        
        self.id_counter = 1
        self.y_current = 340 # Generous 60px gap below lifeline header box (y=240..280)
        
        self.lifeline_events = {l["name"]: [] for l in lifelines}
        self.messages = []
        self.frames = []
        self.self_calls = []

    def get_id(self):
        val = f"cell_{self.id_counter}"
        self.id_counter += 1
        return val

    def get_cx(self, name):
        for l in self.lifelines:
            if l["name"] == name:
                return l["cx"]
        raise ValueError(f"Lifeline '{name}' not found")

    def add_message(self, num_str, label, from_name, to_name, is_return=False):
        y = self.y_current
        from_cx = self.get_cx(from_name)
        to_cx = self.get_cx(to_name)
        
        self.lifeline_events[from_name].append(y)
        self.lifeline_events[to_name].append(y)
        
        if to_cx > from_cx:
            sx = from_cx + 5
            tx = to_cx - 5
        else:
            sx = from_cx - 5
            tx = to_cx + 5
            
        msg = {
            "id": self.get_id(),
            "text": esc(f"{num_str}. {label}" if num_str else label),
            "sx": sx,
            "sy": y,
            "tx": tx,
            "ty": y,
            "is_return": is_return
        }
        self.messages.append(msg)
        self.y_current += 42
        return y

    def add_self_call(self, num_str, label, lifeline_name):
        y = self.y_current
        cx = self.get_cx(lifeline_name)
        self.lifeline_events[lifeline_name].append(y)
        self.lifeline_events[lifeline_name].append(y + 24)
        
        sc = {
            "id": self.get_id(),
            "act_id": self.get_id(),
            "text": esc(f"{num_str}. {label}" if num_str else label),
            "cx": cx,
            "y": y,
            "lifeline_name": lifeline_name
        }
        self.self_calls.append(sc)
        
        # Advance y_current past the self-call arrow (24px) + small act (28px)
        self.y_current += 54
        return y

    def start_opt_frame(self, guard, from_lifeline, to_lifeline):
        from_cx = self.get_cx(from_lifeline)
        to_cx = self.get_cx(to_lifeline)
        min_x = min(from_cx, to_cx) - 80
        max_x = max(from_cx, to_cx) + 80
        
        opt_start_y = self.y_current + 16 # Exactly 70px below self-call start, 28px below small activation
        
        frame = {
            "type": "opt",
            "frame_id": self.get_id(),
            "guard_id": self.get_id(),
            "guard": esc(f"[{guard}]"),
            "x": min_x,
            "y": opt_start_y,
            "width": max_x - min_x,
            "height": 0
        }
        self.frames.append(frame)
        self.y_current = opt_start_y + 45
        return frame

    def end_opt_frame(self, frame):
        last_msg_y = self.y_current - 42
        frame["height"] = (last_msg_y + 28) - frame["y"]
        self.y_current = frame["y"] + frame["height"] + 35

    def start_alt_frame(self, guard1, guard2, from_lifeline, to_lifeline):
        from_cx = self.get_cx(from_lifeline)
        to_cx = self.get_cx(to_lifeline)
        min_x = min(from_cx, to_cx) - 80
        max_x = max(from_cx, to_cx) + 80
        
        alt_start_y = self.y_current + 16
        frame = {
            "type": "alt",
            "frame_id": self.get_id(),
            "guard1_id": self.get_id(),
            "guard2_id": self.get_id(),
            "line_id": self.get_id(),
            "guard1": esc(f"[{guard1}]"),
            "guard2": esc(f"[{guard2}]"),
            "x": min_x,
            "y": alt_start_y,
            "split_y": 0,
            "width": max_x - min_x,
            "height": 0
        }
        self.frames.append(frame)
        self.y_current = alt_start_y + 45
        return frame

    def split_alt_frame(self, frame):
        last_msg_y = self.y_current - 42
        split_y = last_msg_y + 28
        frame["split_y"] = split_y
        self.y_current = split_y + 45

    def end_alt_frame(self, frame):
        last_msg_y = self.y_current - 42
        frame["height"] = (last_msg_y + 28) - frame["y"]
        self.y_current = frame["y"] + frame["height"] + 35

    def build_xml(self):
        y_bottom = self.y_current + 90
        total_page_h = max(self.page_height, y_bottom + 100)
        
        xml = f'''<mxGraphModel dx="1277" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{self.page_width}" pageHeight="{total_page_h}" math="0" shadow="0">
  <root>
    <mxCell id="0" />
    <mxCell id="1" parent="0" />
'''
        # (b) Lifelines top headers only (bottom duplicate boxes/actors/dbs removed per user request)
        lifeline_top_ids = {}
        for l in self.lifelines:
            name = l["name"]
            escaped_name = esc(name)
            l_type = l["type"]
            cx = l["cx"]
            
            if l_type == "actor":
                top_id = self.get_id()
                dash_id = self.get_id()
                lifeline_top_ids[name] = top_id
                
                # Top actor
                xml += f'''    <mxCell id="{top_id}" value="{escaped_name}" style="shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;" parent="1" vertex="1">
      <mxGeometry x="{cx - 15}" y="170" width="30" height="60" as="geometry" />
    </mxCell>
'''
                # Dashed line extending down to bottom
                xml += f'''    <mxCell id="{dash_id}" value="" style="endArrow=none;dashed=1;html=1;rounded=0;" parent="1" edge="1">
      <mxGeometry relative="1" as="geometry">
        <mxPoint x="{cx}" y="230" as="sourcePoint" />
        <mxPoint x="{cx}" y="{y_bottom}" as="targetPoint" />
      </mxGeometry>
    </mxCell>
'''
            elif l_type == "db":
                top_id = self.get_id()
                dash_id = self.get_id()
                lifeline_top_ids[name] = top_id
                
                # Top DB
                xml += f'''    <mxCell id="{top_id}" value="{escaped_name}" style="shape=mxgraph.flowchart.database;whiteSpace=wrap;strokeWidth=2;fontSize=11;" parent="1" vertex="1">
      <mxGeometry x="{cx - 35}" y="180" width="70" height="60" as="geometry" />
    </mxCell>
'''
                # Dashed line extending down to bottom
                xml += f'''    <mxCell id="{dash_id}" value="" style="endArrow=none;dashed=1;html=1;rounded=0;" parent="1" edge="1">
      <mxGeometry relative="1" as="geometry">
        <mxPoint x="{cx}" y="240" as="sourcePoint" />
        <mxPoint x="{cx}" y="{y_bottom}" as="targetPoint" />
      </mxGeometry>
    </mxCell>
'''
            else: # Standard lifeline
                top_id = self.get_id()
                lifeline_top_ids[name] = top_id
                height = y_bottom - 240
                
                xml += f'''    <mxCell id="{top_id}" value="{escaped_name}" style="shape=umlLifeline;perimeter=lifelinePerimeter;whiteSpace=wrap;html=1;container=1;dropTarget=0;collapsible=0;recursiveResize=0;outlineConnect=0;portConstraint=eastwest;newEdgeStyle={{&quot;curved&quot;:0,&quot;rounded&quot;:0}};" parent="1" vertex="1">
      <mxGeometry x="{cx - 70}" y="240" width="140" height="{height}" as="geometry" />
    </mxCell>
'''

        # (c) Activations
        for l in self.lifelines:
            name = l["name"]
            cx = l["cx"]
            events = self.lifeline_events[name]
            if not events:
                continue
            min_y = min(events)
            max_y = max(events)
            act_id = self.get_id()
            
            if l["type"] in ("actor", "db"):
                xml += f'''    <mxCell id="{act_id}" value="" style="html=1;points=[[0,0,0,0,5],[0,1,0,0,-5],[1,0,0,0,5],[1,1,0,0,-5]];perimeter=orthogonalPerimeter;outlineConnect=0;targetShapes=umlLifeline;portConstraint=eastwest;newEdgeStyle={{&quot;curved&quot;:0,&quot;rounded&quot;:0}};fontSize=13;" parent="1" vertex="1">
      <mxGeometry x="{cx - 5}" y="{min_y}" width="10" height="{max_y - min_y}" as="geometry" />
    </mxCell>
'''
            else:
                top_id = lifeline_top_ids[name]
                rel_y = min_y - 240
                rel_h = max_y - min_y
                xml += f'''    <mxCell id="{act_id}" value="" style="html=1;points=[[0,0,0,0,5],[0,1,0,0,-5],[1,0,0,0,5],[1,1,0,0,-5]];perimeter=orthogonalPerimeter;outlineConnect=0;targetShapes=umlLifeline;portConstraint=eastwest;newEdgeStyle={{&quot;curved&quot;:0,&quot;rounded&quot;:0}};fontSize=13;" parent="{top_id}" vertex="1">
      <mxGeometry x="65" y="{rel_y}" width="10" height="{rel_h}" as="geometry" />
    </mxCell>
'''

        # (d) Self-call small sub-activations (nested on the right edge, matching UML standard)
        for sc in self.self_calls:
            l_name = sc["lifeline_name"]
            l_info = next(x for x in self.lifelines if x["name"] == l_name)
            if l_info["type"] in ("actor", "db"):
                xml += f'''    <mxCell id="{sc['act_id']}" value="" style="html=1;perimeter=orthogonalPerimeter;outlineConnect=0;fillColor=#ffffff;strokeColor=#000000;" parent="1" vertex="1">
      <mxGeometry x="{sc['cx']}" y="{sc['y'] + 14}" width="10" height="28" as="geometry" />
    </mxCell>
'''
            else:
                top_id = lifeline_top_ids[l_name]
                rel_y = sc["y"] + 14 - 240
                xml += f'''    <mxCell id="{sc['act_id']}" value="" style="html=1;perimeter=orthogonalPerimeter;outlineConnect=0;fillColor=#ffffff;strokeColor=#000000;" parent="{top_id}" vertex="1">
      <mxGeometry x="70" y="{rel_y}" width="10" height="28" as="geometry" />
    </mxCell>
'''

        # (e) Frames (opt / alt)
        for f in self.frames:
            if f["type"] == "opt":
                xml += f'''    <mxCell id="{f['frame_id']}" value="opt" style="shape=umlFrame;whiteSpace=wrap;html=1;pointerEvents=0;width=70;height=20;fontSize=13;fillColor=none;" parent="1" vertex="1">
      <mxGeometry x="{f['x']}" y="{f['y']}" width="{f['width']}" height="{f['height']}" as="geometry" />
    </mxCell>
    <mxCell id="{f['guard_id']}" value="{f['guard']}" style="text;align=center;fontStyle=1;verticalAlign=middle;strokeColor=none;html=1;fontSize=13;" parent="1" vertex="1">
      <mxGeometry x="{f['x'] + 85}" y="{f['y']}" width="250" height="24" as="geometry" />
    </mxCell>
'''
            elif f["type"] == "alt":
                xml += f'''    <mxCell id="{f['frame_id']}" value="alt" style="shape=umlFrame;whiteSpace=wrap;html=1;pointerEvents=0;width=70;height=20;fontSize=13;fillColor=none;" parent="1" vertex="1">
      <mxGeometry x="{f['x']}" y="{f['y']}" width="{f['width']}" height="{f['height']}" as="geometry" />
    </mxCell>
    <mxCell id="{f['guard1_id']}" value="{f['guard1']}" style="text;align=center;fontStyle=1;verticalAlign=middle;strokeColor=none;html=1;fontSize=13;" parent="1" vertex="1">
      <mxGeometry x="{f['x'] + 85}" y="{f['y']}" width="200" height="24" as="geometry" />
    </mxCell>
    <mxCell id="{f['line_id']}" value="" style="endArrow=none;dashed=1;html=1;rounded=0;" parent="1" edge="1">
      <mxGeometry relative="1" as="geometry">
        <mxPoint x="{f['x']}" y="{f['split_y']}" as="sourcePoint" />
        <mxPoint x="{f['x'] + f['width']}" y="{f['split_y']}" as="targetPoint" />
      </mxGeometry>
    </mxCell>
    <mxCell id="{f['guard2_id']}" value="{f['guard2']}" style="text;align=center;fontStyle=1;verticalAlign=middle;strokeColor=none;html=1;fontSize=13;" parent="1" vertex="1">
      <mxGeometry x="{f['x'] + 85}" y="{f['split_y']}" width="200" height="24" as="geometry" />
    </mxCell>
'''

        # (f) Self-call message loops (connecting to right side of sub-activation)
        for sc in self.self_calls:
            xml += f'''    <mxCell id="{sc['id']}" value="{sc['text']}" style="html=1;verticalAlign=bottom;align=left;endArrow=block;curved=0;rounded=0;fontSize=12;labelBackgroundColor=#ffffff;" parent="1" edge="1">
      <mxGeometry relative="1" as="geometry">
        <Array as="points">
          <mxPoint x="{sc['cx'] + 35}" y="{sc['y']}" />
          <mxPoint x="{sc['cx'] + 35}" y="{sc['y'] + 24}" />
        </Array>
        <mxPoint x="{sc['cx'] + 5}" y="{sc['y']}" as="sourcePoint" />
        <mxPoint x="{sc['cx'] + 10}" y="{sc['y'] + 24}" as="targetPoint" />
      </mxGeometry>
    </mxCell>
'''

        # (g) Messages
        for m in self.messages:
            if m["is_return"]:
                style = "html=1;verticalAlign=bottom;endArrow=open;dashed=1;endSize=8;curved=0;rounded=0;fontSize=12;labelBackgroundColor=#ffffff;"
            else:
                style = "html=1;verticalAlign=bottom;endArrow=block;curved=0;rounded=0;fontSize=12;labelBackgroundColor=#ffffff;"
                
            xml += f'''    <mxCell id="{m['id']}" value="{m['text']}" style="{style}" parent="1" edge="1">
      <mxGeometry relative="1" as="geometry">
        <mxPoint x="{m['sx']}" y="{m['sy']}" as="sourcePoint" />
        <mxPoint x="{m['tx']}" y="{m['ty']}" as="targetPoint" />
      </mxGeometry>
    </mxCell>
'''

        xml += '''  </root>
</mxGraphModel>'''
        return xml
