ENTRY(rain_step)
SECTIONS {
 .helpers 0x394c : { *(.text.render_tail) }
 ASSERT(SIZEOF(.helpers)<=140,"render tail overflows tick adapter")
 .tick_adapter 0x39d8 : { KEEP(*(.text.tick_adapter)) }
 ASSERT(SIZEOF(.tick_adapter)<=32,"tick adapter overflow")
 .routes 0x3b12 : { *(.rodata.routes) *(.rodata.conflicts) *(.rodata.inverse) *(.text.rnd) }
 ASSERT(SIZEOF(.routes)<=98,"route/helper area overflow")
 .init_adapter 0x3bf8 : { KEEP(*(.text.init_adapter)) }
 ASSERT(SIZEOF(.init_adapter)<=50,"init adapter overflow")
 .dispatch_adapter 0x3f50 : { KEEP(*(.text.dispatch_adapter)) }
 ASSERT(SIZEOF(.dispatch_adapter)==10,"dispatch adapter must be exactly ten bytes")
 .render 0x4c6c : { *(.text.render_head) }
 ASSERT(SIZEOF(.render)<=189,"render head overflows brightness table")
 .main 0x523c : { *(.text.rain_step) *(.rodata.curve) *(.rodata.cst32) }
 ASSERT(SIZEOF(.main)<=424,"main overflows call adapter")
 .call_adapter 0x53e4 : { KEEP(*(.text.call_adapter)) }
 ASSERT(SIZEOF(.call_adapter)<=28,"call adapter overflows configuration")
 /DISCARD/ : { *(.text.rain_init) *(.comment*) *(.ARM.exidx*) *(.ARM.extab*) }
}
