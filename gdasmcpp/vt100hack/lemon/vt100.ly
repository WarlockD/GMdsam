
escape ::= 1B.
csi_entry::= escape 5B.
csi_intermediate_collect::= 20 | 22 | 23 | 24 | 25 | 26 | 27 | 28 | 29 | 2A | 2B | 2C | 2D | 2E | 2F.
csi_intermediate::= csi_entry csi_intermediate_collect.
csi_intermediate::= csi_intermediate csi_intermediate_collect.
csi_intermediate_ignore ::= 30 | 32 | 33 | 34 | 35 | 36 | 37 | 38 | 39 | 3A | 3B | 3C | 3D | 3E | 3F.
csi_intermediate_dispatch ::= M.
csi_ignore::= csi_intermediate csi_intermediate_ignore.

csi_dispatch::= csi_intermediate csi_intermediate_dispatch.
ground ::= csi_dispatch.

