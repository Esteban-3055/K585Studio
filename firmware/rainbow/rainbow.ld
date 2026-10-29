ENTRY(rain_step)
SECTIONS {
 .helpers 0x394c : { *(.text.pixel) *(.text.rain_init) *(.text.rnd) }
 ASSERT(SIZEOF(.helpers)<=140,"helper overflow into gated adapter")
 .routes 0x3b12 : { *(.rodata.routes) }
 ASSERT(SIZEOF(.routes)<=98,"route area overflow")
 .render 0x4c6c : { *(.text.render) }
 ASSERT(SIZEOF(.render)<=189,"render area overflow")
 .main 0x523c : { *(.text.rain_step) *(.rodata.curve) *(.rodata.cst32) }
 ASSERT(SIZEOF(.main)<=424,"core overflow into call adapter")
 /DISCARD/ : { *(.comment*) *(.ARM.exidx*) *(.ARM.extab*) }
}
