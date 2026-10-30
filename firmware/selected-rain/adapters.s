.syntax unified
.cpu cortex-m0
.thumb
.section .text.init_adapter,"ax",%progbits
.p2align 2
.global rain_init_adapter
.thumb_func
rain_init_adapter:
 cmp r0,#23
 bne init_done
 ldr r0,=0x20000056
 ldrb r0,[r0]
 cmp r0,#1
 bne init_done
 ldr r0,=0x20000408
 movs r1,#0
 movs r2,#52
init_clear:
 str r1,[r0,r2]
 subs r2,#4
 bpl init_clear
 movs r1,#1
 strh r1,[r0,#40]
 movs r1,#255
 movs r2,#34
init_routes:
 strb r1,[r0,r2]
 subs r2,#8
 bpl init_routes
init_done:
 bx lr
.ltorg

.section .text.tick_adapter,"ax",%progbits
.p2align 2
.global rain_tick_adapter
.thumb_func
rain_tick_adapter:
 @ These two native literals are preserved in the audited base ROM.
 ldr r0,[pc,#508] @ 0x3bd8 contains 0x2000005a; subtract four for profile.
 subs r0,#4
 ldrb r0,[r0]
 cmp r0,#1
 bne tick_done
 ldr r0,[pc,#432] @ 0x3b94 contains 0x2000003b, native frame counter.
 ldrb r1,[r0]
 cmp r1,#5
 bls tick_done
 movs r1,#0
 strb r1,[r0]
 ldr r3,=0x53e5
 bx r3
tick_done:
 bx lr
.ltorg

.section .text.call_adapter,"ax",%progbits
.p2align 2
.global rain_call_adapter
.thumb_func
rain_call_adapter:
 push {lr}
 ldr r0,=0x20000408
 ldr r1,=0x200000dc
 ldr r3,=0x20000180
 bl rain_step
 pop {pc}
 .short 0xbf00 @ Match the reviewed Thumb NOP padding before the literal pool.
.ltorg

.section .text.dispatch_adapter,"ax",%progbits
.global rain_dispatch_adapter
.thumb_func
rain_dispatch_adapter:
 cmp r0,#23
 bne dispatch_done
 bl rain_tick_adapter
dispatch_done:
 pop {pc}
