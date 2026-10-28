# -*- coding: utf-8 -*-
import xml.sax.saxutils as saxutils

def esc(text):
    if text is None:
        return ""
    return saxutils.escape(str(text), {'"': "&quot;", "'": "&apos;"})

class ClassDiagramBuilder:
    def __init__(self, title, page_width=1400, page_height=1300):
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

    def add_class(self, class_id, name, attributes=None, methods=None, x=100, y=100, width=240, is_interface=False):
        if attributes is None:
            attributes = []
        if methods is None:
            methods = []
            
        attr_text = "\n".join(attributes) if attributes else ""
        meth_text = "\n".join(methods) if methods else ""
        
        # Calculate heights based on content
        header_h = 36 if is_interface else 26
        attr_h = max(24, len(attributes) * 16 + 8) if attributes else 20
        meth_h = max(24, len(methods) * 16 + 8) if methods else 20
        total_h = header_h + attr_h + 8 + meth_h
        
        header_val = f"&lt;&lt;interface&gt;&gt;&lt;br/&gt;&lt;b&gt;{name}&lt;/b&gt;" if is_interface else f"&lt;b&gt;{name}&lt;/b&gt;"
        
        c = {
            "id": class_id,
            "name": name,
            "header_val": header_val,
            "attr_val": esc(attr_text),
            "meth_val": esc(meth_text),
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
        # Source (child) -> Target (base class)
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
        # Source (impl) -> Target (interface)
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

    def add_association(self, source_id, target_id, label="Association", mult_source=None, mult_target=None):
        edge_id = self.get_id()
        self.edges.append({
            "id": edge_id,
            "type": "association",
            "source": source_id,
            "target": target_id,
            "label": label,
            "mult_source": mult_source,
            "mult_target": mult_target,
            "lbl_src_id": self.get_id() if mult_source else None,
            "lbl_tgt_id": self.get_id() if mult_target else None,
            "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=none;strokeColor=#000000;fontSize=11;labelBackgroundColor=#ffffff;"
        })

    def build_xml(self):
        xml = f'''<mxGraphModel dx="1277" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{self.page_width}" pageHeight="{self.page_height}" math="0" shadow="0">
  <root>
    <mxCell id="0" />
    <mxCell id="1" parent="0" />
'''
        # Add classes
        for c in self.classes:
            xml += f'''    <mxCell id="{c['id']}" value="{c['header_val']}" style="swimlane;fontStyle=0;align=center;verticalAlign=top;childLayout=stackLayout;horizontal=1;startSize={c['header_h']};horizontalStack=0;resizeParent=1;resizeParentMax=0;resizeLast=0;collapsible=0;marginBottom=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#000000;" vertex="1" parent="1">
      <mxGeometry x="{c['x']}" y="{c['y']}" width="{c['width']}" height="{c['height']}" as="geometry" />
    </mxCell>
    <mxCell id="{c['attr_id']}" value="{c['attr_val']}" style="text;strokeColor=none;fillColor=none;align=left;verticalAlign=top;spacingLeft=6;spacingRight=4;overflow=hidden;rotatable=0;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;whiteSpace=wrap;html=1;fontSize=11;" vertex="1" parent="{c['id']}">
      <mxGeometry y="{c['header_h']}" width="{c['width']}" height="{c['attr_h']}" as="geometry" />
    </mxCell>
    <mxCell id="{c['sep_id']}" value="" style="line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;spacingLeft=3;spacingRight=3;rotatable=0;labelPosition=right;points=[];portConstraint=eastwest;strokeColor=inherit;" vertex="1" parent="{c['id']}">
      <mxGeometry y="{c['header_h'] + c['attr_h']}" width="{c['width']}" height="8" as="geometry" />
    </mxCell>
    <mxCell id="{c['meth_id']}" value="{c['meth_val']}" style="text;strokeColor=none;fillColor=none;align=left;verticalAlign=top;spacingLeft=6;spacingRight=4;overflow=hidden;rotatable=0;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;whiteSpace=wrap;html=1;fontSize=11;" vertex="1" parent="{c['id']}">
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
            if e.get("mult_source"):
                xml += f'''    <mxCell id="{e['lbl_src_id']}" value="{esc(e['mult_source'])}" style="edgeLabel;html=1;align=left;verticalAlign=bottom;resizable=0;points=[];fontSize=11;" vertex="1" connectable="0" parent="{e['id']}">
      <mxGeometry x="-0.8" y="-10" relative="1" as="geometry">
        <mxPoint as="offset" />
      </mxGeometry>
    </mxCell>
'''
            if e.get("mult_target"):
                xml += f'''    <mxCell id="{e['lbl_tgt_id']}" value="{esc(e['mult_target'])}" style="edgeLabel;html=1;align=right;verticalAlign=bottom;resizable=0;points=[];fontSize=11;" vertex="1" connectable="0" parent="{e['id']}">
      <mxGeometry x="0.8" y="-10" relative="1" as="geometry">
        <mxPoint as="offset" />
      </mxGeometry>
    </mxCell>
'''

        xml += '''  </root>
</mxGraphModel>'''
        return xml

if __name__ == "__main__":
    # Test with VerifyEmail from user sample
    b = ClassDiagramBuilder("VerifyEmail_Sample", 1100, 1200)
    
    b.add_class("cb", "ControllerBase", x=440, y=40, width=160)
    
    b.add_class("dto", "VerifyEmailDTO", attributes=[
        "+Email: string",
        "+OTP: string"
    ], x=60, y=160, width=200)
    
    b.add_class("ctrl", "AuthenticationController", attributes=[
        "-IAuthenticationService _authService",
        "-ILogger<AuthenticationController> _logger"
    ], methods=[
        "+VerifyEmail(dto: VerifyEmailDTO): Task<IActionResult>"
    ], x=360, y=160, width=320)
    
    b.add_class("isvc", "IAuthenticationService", methods=[
        "+VerifyEmailAsync(dto: VerifyEmailDTO): Task",
        ".."
    ], x=360, y=320, width=320, is_interface=True)
    
    b.add_class("svc", "AuthenticationService", attributes=[
        "-IUserRepository _userRepository",
        "-IMemoryCache _cache",
        "-ILogger<AuthenticationService> _logger"
    ], methods=[
        "+VerifyEmailAsync(dto: VerifyEmailDTO): Task"
    ], x=330, y=470, width=380)
    
    b.add_class("pending", "PendingRegistration", attributes=[
        "+FullName: string",
        "+Email: string",
        "+PhoneNumber: string?",
        "+PasswordHash: string",
        "+OtpCode: string",
        "+ExpiredAt: DateTime"
    ], x=780, y=470, width=200)
    
    b.add_class("irepo", "IUserRepository", methods=[
        "+GetRoleByNameAsync(roleName: string): Task<Role?>",
        "+AddAsync(user: User): Task",
        "+AddUserRoleAsync(userRole: UserRole): Task",
        "+SaveChangesAsync(): Task",
        "..."
    ], x=360, y=650, width=320, is_interface=True)
    
    b.add_class("user", "User", attributes=[
        "-UserId: int",
        "-Email: string",
        "-PasswordHash: string",
        "-FullName: string",
        "-PhoneNumber: string?",
        "-AvatarUrl: string?",
        "-IsVerified: bool",
        "-OtpHash: string?",
        "-OtpExpiredAt: DateTime?",
        "-EkycStatus: string",
        "-CreatedAt: DateTime",
        "-IsActive: bool",
        "-AuthProvider: string",
        "-HasPassword: bool"
    ], x=120, y=860, width=200)
    
    b.add_class("userrole", "UserRole", attributes=[
        "+UserId: int",
        "+RoleId: int",
        "+AssignedAt: DateTime"
    ], x=420, y=880, width=200)
    
    b.add_class("role", "Role", attributes=[
        "+RoleId: int",
        "+RoleName: string"
    ], x=720, y=880, width=200)
    
    # Relationships
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "pending", "Dependency")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_dependency("irepo", "user", "Dependency")
    b.add_dependency("irepo", "userrole", "Dependency")
    b.add_dependency("irepo", "role", "Dependency")
    b.add_association("user", "userrole", "Association", mult_source="1", mult_target="0..*")
    b.add_association("userrole", "role", "Association", mult_source="0..*", mult_target="1")
    
    xml = b.build_xml()
    with open("scratch/sample_verify_email.xml", "w", encoding="utf-8") as f:
        f.write(xml)
    print("Sample XML created, size:", len(xml))
