%pure-parser

//%union { int value; }
//%type<value>  range_digits range_20_2F range_30_3F range_3C_3F range_40_7E range_40_7E param 
//%type<value>  csi_intermediate_collect

%start anywhere
%%
anywhere: 
| anywhere escape {};

escape: '\x1B' { clear(); mode(Mode::Escape); };

csi_entry: '\x9B' | escape '\x5B' { clear(); mode(Mode::Csi); };


csi_param: csi_entry range_3C_3F { collect($2);};
csi_param: csi_entry param { add_param($2);};
csi_param: csi_param param { add_param($2);};

csi_dispatch: csi_entry range_40_7E { dispatch($2);};
csi_dispatch: csi_param range_40_7E { dispatch($2);};
csi_dispatch: csi_intermediate range_40_7E { dispatch($2);};

csi_intermediate: csi_entry range_20_2F {  collect($2); };
csi_intermediate: csi_param  range_20_2F { collect($2);};

csi_ignore:  csi_param range_3C_3F | csi_param '\x3f';
csi_ignore:  csi_intermediate range_30_3F;
csi_ignore: csi_entry '\x3a';

ground: csi_ignore range_40_7E { clear(); };
ground: csi_dispatch { clear(); };

mike: '0' { $$ = $1;};
range_digits: '0' | '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' { $$ = $1; };
range_20_2F: ' ' | '!' | '\"' | '#' | '$' | '%' | '&' | '\'' | '(' | ')' | '*' | '+' | ',' | '-' | '.' | '/' { $$ = $1; };
range_30_3F: range_digits | ':' | ';' | '<' | '=' | '>' | '?' { $$ = $1; };
range_3C_3F: '<' | '=' | '>' | '?' { $$ = $1; };

range_40_7E: 'a' | 'b' | 'c' | 'd' | 'e' | 'f' { $$ = $1;};
param: range_digits | ';' { $$ = $1; };

