	.text
	.syntax unified
	.eabi_attribute	67, "2.09"	@ Tag_conformance
	.cpu	cortex-m0
	.eabi_attribute	6, 12	@ Tag_CPU_arch
	.eabi_attribute	7, 77	@ Tag_CPU_arch_profile
	.eabi_attribute	8, 0	@ Tag_ARM_ISA_use
	.eabi_attribute	9, 1	@ Tag_THUMB_ISA_use
	.eabi_attribute	34, 0	@ Tag_CPU_unaligned_access
	.eabi_attribute	17, 1	@ Tag_ABI_PCS_GOT_use
	.eabi_attribute	20, 1	@ Tag_ABI_FP_denormal
	.eabi_attribute	21, 0	@ Tag_ABI_FP_exceptions
	.eabi_attribute	23, 3	@ Tag_ABI_FP_number_model
	.eabi_attribute	24, 1	@ Tag_ABI_align_needed
	.eabi_attribute	25, 1	@ Tag_ABI_align_preserved
	.eabi_attribute	38, 1	@ Tag_ABI_FP_16bit_format
	.eabi_attribute	18, 4	@ Tag_ABI_PCS_wchar_t
	.eabi_attribute	26, 2	@ Tag_ABI_enum_size
	.eabi_attribute	14, 0	@ Tag_ABI_PCS_R9_use
	.file	"final-source.c"
	.section	.text.rain_init,"ax",%progbits
	.globl	rain_init                       @ -- Begin function rain_init
	.p2align	2
	.type	rain_init,%function
	.code	16                              @ @rain_init
	.thumb_func
rain_init:
@ %bb.0:
	movs	r1, #0
	mov	r2, r1
.LBB0_1:                                @ =>This Inner Loop Header: Depth=1
	strb	r1, [r0, r2]
	adds	r2, r2, #1
	cmp	r2, #56
	bne	.LBB0_1
@ %bb.2:
	ldr	r1, .LCPI0_0
	strh	r1, [r0, #40]
	movs	r1, #2
.LBB0_3:                                @ =>This Inner Loop Header: Depth=1
	movs	r2, #255
	strb	r2, [r0, r1]
	adds	r1, #8
	cmp	r1, #42
	bne	.LBB0_3
@ %bb.4:
	bx	lr
	.p2align	2
@ %bb.5:
.LCPI0_0:
	.long	44257                           @ 0xace1
.Lfunc_end0:
	.size	rain_init, .Lfunc_end0-rain_init
                                        @ -- End function
	.section	.text.rain_step,"ax",%progbits
	.globl	rain_step                       @ -- Begin function rain_step
	.p2align	2
	.type	rain_step,%function
	.code	16                              @ @rain_step
	.thumb_func
rain_step:
@ %bb.0:
	push	{r4, r5, r6, r7, lr}
	sub	sp, #20
	ldr	r1, .LCPI1_0
	ldrb	r2, [r1]
	str	r0, [sp, #12]                   @ 4-byte Spill
	mov	r4, r0
	adds	r4, #51
	ldrb	r1, [r4, #3]
	strb	r2, [r4, #3]
	subs	r0, r2, r1
	uxtb	r0, r0
	lsls	r0, r0, #5
	ldrb	r1, [r4, #1]
	adds	r0, r0, r1
	cmp	r0, #75
	blo	.LBB1_6
@ %bb.1:
	ldrb	r1, [r4, #2]
.LBB1_2:                                @ =>This Inner Loop Header: Depth=1
	adds	r1, r1, #1
	uxtb	r2, r1
	cmp	r2, #192
	bne	.LBB1_4
@ %bb.3:                                @   in Loop: Header=BB1_2 Depth=1
	movs	r1, #0
.LBB1_4:                                @   in Loop: Header=BB1_2 Depth=1
	subs	r0, #75
	uxth	r2, r0
	cmp	r2, #74
	bhi	.LBB1_2
@ %bb.5:
	strb	r1, [r4, #2]
.LBB1_6:
	str	r4, [sp, #4]                    @ 4-byte Spill
	strb	r0, [r4, #1]
	movs	r0, #0
	mov	r1, r0
.LBB1_7:                                @ =>This Inner Loop Header: Depth=1
	strb	r0, [r3, r1]
	adds	r1, r1, #1
	movs	r2, #81
	lsls	r2, r2, #2
	cmp	r1, r2
	bne	.LBB1_7
@ %bb.8:
	str	r3, [sp, #8]                    @ 4-byte Spill
	movs	r0, #60
	str	r0, [sp, #16]                   @ 4-byte Spill
	movs	r5, #255
	movs	r6, #0
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	mov	r1, r6
.LBB1_9:                                @ =>This Inner Loop Header: Depth=1
	ldrb	r2, [r0, #2]
	cmp	r2, #255
	beq	.LBB1_15
@ %bb.10:                               @   in Loop: Header=BB1_9 Depth=1
	ldrh	r3, [r0]
	adds	r7, r3, #1
	strh	r7, [r0]
	ldrb	r3, [r0, #7]
	lsls	r3, r3, #1
	adds	r3, #26
	uxtb	r3, r3
	movs	r4, #5
	muls	r4, r3, r4
	uxth	r7, r7
	cmp	r4, r7
	bls	.LBB1_14
@ %bb.11:                               @   in Loop: Header=BB1_9 Depth=1
	ldr	r4, [sp, #16]                   @ 4-byte Reload
	uxtb	r7, r4
	cmp	r3, r7
	bhi	.LBB1_13
@ %bb.12:                               @   in Loop: Header=BB1_9 Depth=1
	mov	r3, r7
.LBB1_13:                               @   in Loop: Header=BB1_9 Depth=1
	movs	r4, #1
	lsls	r4, r2
	uxth	r2, r4
	orrs	r6, r2
	str	r3, [sp, #16]                   @ 4-byte Spill
	b	.LBB1_16
.LBB1_14:                               @   in Loop: Header=BB1_9 Depth=1
	movs	r2, #255
	strb	r2, [r0, #2]
.LBB1_15:                               @   in Loop: Header=BB1_9 Depth=1
	uxtb	r5, r1
.LBB1_16:                               @   in Loop: Header=BB1_9 Depth=1
	adds	r0, #8
	adds	r1, r1, #1
	cmp	r1, #5
	bne	.LBB1_9
@ %bb.17:
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	ldrh	r1, [r0, #42]
	cmp	r1, #0
	beq	.LBB1_20
@ %bb.18:
	subs	r1, r1, #1
.LBB1_19:
	strh	r1, [r0, #42]
	b	.LBB1_30
.LBB1_20:
	bl	rnd
	movs	r1, #7
	ands	r1, r0
	adds	r1, r1, #7
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	strh	r1, [r0, #42]
	cmp	r5, #255
	beq	.LBB1_30
@ %bb.21:
	movs	r7, #30
	ldr	r4, .LCPI1_1
.LBB1_22:                               @ =>This Inner Loop Header: Depth=1
	bl	rnd
	movs	r1, #15
	ands	r1, r0
	cmp	r1, #13
	bhi	.LBB1_29
@ %bb.23:                               @   in Loop: Header=BB1_22 Depth=1
	lsls	r0, r1, #1
	ldrh	r0, [r4, r0]
	tst	r0, r6
	bne	.LBB1_29
@ %bb.24:                               @   in Loop: Header=BB1_22 Depth=1
	cmp	r1, #13
	beq	.LBB1_26
@ %bb.25:                               @   in Loop: Header=BB1_22 Depth=1
	adds	r0, r1, #1
	lsrs	r3, r0, #1
	b	.LBB1_27
.LBB1_26:                               @   in Loop: Header=BB1_22 Depth=1
	movs	r3, #0
.LBB1_27:                               @   in Loop: Header=BB1_22 Depth=1
	movs	r2, #3
	muls	r2, r1, r2
	ldr	r0, .LCPI1_2
	adds	r2, r0, r2
	ldrb	r2, [r2, #2]
	lsrs	r2, r2, #4
	mov	r0, r3
	cmp	r3, #6
	beq	.LBB1_31
@ %bb.28:                               @   in Loop: Header=BB1_22 Depth=1
	ldr	r3, [sp, #12]                   @ 4-byte Reload
	adds	r3, r3, r0
	movs	r4, #44
	ldrb	r3, [r3, r4]
	ldr	r4, .LCPI1_1
	cmp	r2, r3
	bne	.LBB1_31
.LBB1_29:                               @   in Loop: Header=BB1_22 Depth=1
	subs	r7, r7, #1
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	bne	.LBB1_22
.LBB1_30:
	ldr	r1, [sp, #8]                    @ 4-byte Reload
	bl	render
	add	sp, #20
	pop	{r4, r5, r6, r7, pc}
.LBB1_31:
	lsls	r3, r5, #3
	movs	r4, #0
	ldr	r6, [sp, #12]                   @ 4-byte Reload
	strh	r4, [r6, r3]
	adds	r5, r6, r3
	strb	r1, [r5, #2]
	adds	r0, r6, r0
	movs	r1, #44
	strb	r2, [r0, r1]
	ldr	r3, [sp, #4]                    @ 4-byte Reload
	ldrb	r1, [r3]
	adds	r0, r1, #5
	uxtb	r2, r0
	cmp	r2, #11
	bls	.LBB1_33
@ %bb.32:
	subs	r0, r1, #7
.LBB1_33:
	strb	r0, [r5, #3]
	strb	r0, [r3]
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	bl	rnd
	movs	r1, #6
	muls	r1, r0, r1
	lsrs	r0, r1, #16
	adds	r0, #18
	strb	r0, [r5, #7]
	lsls	r5, r0, #1
	adds	r5, #26
	ldr	r0, [sp, #16]                   @ 4-byte Reload
	uxtb	r0, r0
	cmp	r5, r0
	bhi	.LBB1_35
@ %bb.34:
	mov	r5, r0
.LBB1_35:
	ldr	r4, [sp, #12]                   @ 4-byte Reload
	mov	r0, r4
	bl	rnd
	movs	r1, #31
	ands	r1, r0
	movs	r2, #106
	muls	r2, r1, r2
	mov	r0, r4
	lsrs	r1, r2, #5
	adds	r1, #205
	muls	r1, r5, r1
	lsrs	r1, r1, #8
	b	.LBB1_19
	.p2align	2
@ %bb.36:
.LCPI1_0:
	.long	536870968                       @ 0x20000038
.LCPI1_1:
	.long	conflicts
.LCPI1_2:
	.long	routes
.Lfunc_end1:
	.size	rain_step, .Lfunc_end1-rain_step
                                        @ -- End function
	.section .text.rnd,"ax",%progbits
	.p2align 1
	.type rnd,%function
	.thumb_func
rnd:
	ldrh r1,[r0,#40]
	lsrs r1,r1,#1
	bcc .Lrnd_even
	movs r2,#45
	lsls r2,r2,#10
	eors r1,r2
.Lrnd_even:
	strh r1,[r0,#40]
	mov r0,r1
	bx lr
	.section	.text.render_head,"ax",%progbits
	.p2align	2                               @ -- Begin function render
	.type	render,%function
	.code	16                              @ @render
	.thumb_func
render:
@ %bb.0:
	push	{r4, r5, r6, r7, lr}
	sub	sp, #36
	str	r1, [sp, #4]                    @ 4-byte Spill
	str	r0, [sp, #8]                    @ 4-byte Spill
	ldr	r0, .LCPI3_0_head
	ldrb	r0, [r0]
	cmp	r0, #4
	blo	.LBB3_2
@ %bb.1:
	movs	r0, #4
.LBB3_2:
	ldr	r1, .LCPI3_1_head
	ldrb	r0, [r0, r1]
	str	r0, [sp, #24]                   @ 4-byte Spill
	ldr	r0, [sp, #8]                    @ 4-byte Reload
	adds	r0, #53
	str	r0, [sp]                        @ 4-byte Spill
	movs	r0, #0
.LBB3_3:                                @ =>This Loop Header: Depth=1
                                        @     Child Loop BB3_7 Depth 2
                                        @       Child Loop BB3_21 Depth 3
	str	r0, [sp, #12]                   @ 4-byte Spill
	lsls	r0, r0, #3
	ldr	r1, [sp, #8]                    @ 4-byte Reload
	adds	r0, r1, r0
	str	r0, [sp, #32]                   @ 4-byte Spill
	ldrb	r0, [r0, #2]
	cmp	r0, #255
	bne	.Lbridge_head1
	bl	.LBB3_23
.Lbridge_head1:
@ %bb.4:                                @   in Loop: Header=BB3_3 Depth=1
	ldr	r0, [sp, #32]                   @ 4-byte Reload
	ldrb	r0, [r0, #3]
	lsls	r0, r0, #4
	ldr	r1, [sp]                        @ 4-byte Reload
	ldrb	r1, [r1]
	adds	r0, r0, r1
	cmp	r0, #191
	bls	.LBB3_6
@ %bb.5:                                @   in Loop: Header=BB3_3 Depth=1
	subs	r0, #192
.LBB3_6:                                @   in Loop: Header=BB3_3 Depth=1
	str	r0, [sp, #20]                   @ 4-byte Spill
	ldr	r0, [sp, #32]                   @ 4-byte Reload
	adds	r1, r0, #2
	adds	r2, r0, #7
	str	r2, [sp, #28]                   @ 4-byte Spill
	ldrb	r0, [r0, #7]
	subs	r0, #18
	uxtb	r0, r0
	str	r0, [sp, #16]                   @ 4-byte Spill
	movs	r3, #0
	ldr	r2, [sp, #4]                    @ 4-byte Reload
.LBB3_7:                                @   Parent Loop BB3_3 Depth=1
                                        @ =>  This Loop Header: Depth=2
                                        @       Child Loop BB3_21 Depth 3
	ldrb	r0, [r1]
	movs	r5, #3
	muls	r0, r5, r0
	ldr	r4, .LCPI3_2_head
	adds	r0, r4, r0
	lsrs	r4, r3, #1
	ldrb	r4, [r0, r4]
	lsls	r0, r3, #31
	lsrs	r0, r0, #29
	lsrs	r4, r0
	movs	r0, #15
	ands	r0, r4
	cmp	r0, #15
	bne	.Lbridge_head2
	bl	.LBB3_22
.Lbridge_head2:
@ %bb.8:                                @   in Loop: Header=BB3_7 Depth=2
	ldr	r4, [sp, #28]                   @ 4-byte Reload
	ldrb	r4, [r4]
	muls	r4, r3, r4
	ldr	r6, [sp, #32]                   @ 4-byte Reload
	ldrh	r6, [r6]
	subs	r6, r6, r4
	cmp	r6, #243
	bls	.Lbridge_head3
	bl	.LBB3_22
.Lbridge_head3:
@ %bb.9:                                @   in Loop: Header=BB3_7 Depth=2
	ldr	r7, .LCPI3_0_head
	ldrb	r4, [r7, #3]
	cmp	r4, #0
	bne	.Lbridge_head4
	bl	.LBB3_15
.Lbridge_head4:
@ %bb.10:                               @   in Loop: Header=BB3_7 Depth=2
.LBB3_10:
	movs	r4, #7
	muls	r4, r3, r4
	ldr	r7, [sp, #20]                   @ 4-byte Reload
	adds	r4, r4, r7
	cmp	r4, #191
	bhi	.Lbridge_head5
	bl	.LBB3_12
.Lbridge_head5:
@ %bb.11:                               @   in Loop: Header=BB3_7 Depth=2

	bl .LBB3_11

	.p2align 2
.LCPI3_0_head:
	.long 536871133
.LCPI3_1_head:
	.long 19753
.LCPI3_2_head:
	.long routes

	.section .text.render_tail,"ax",%progbits
	.p2align 2
.LBB3_11:
	subs	r4, #192
.LBB3_12:                               @   in Loop: Header=BB3_7 Depth=2
	cmp	r4, #191
	bls	.LBB3_14
@ %bb.13:                               @   in Loop: Header=BB3_7 Depth=2
.LBB3_13:
	subs	r4, #192
.LBB3_14:                               @   in Loop: Header=BB3_7 Depth=2
	muls	r5, r4, r5
	ldr	r4, .LCPI3_3_tail
	adds	r5, r5, r4
	b	.LBB3_16
.LBB3_15:                               @   in Loop: Header=BB3_7 Depth=2
	adds	r5, r7, #4
.LBB3_16:                               @   in Loop: Header=BB3_7 Depth=2
	cmp	r6, #111
	blo	.LBB3_19
@ %bb.17:                               @   in Loop: Header=BB3_7 Depth=2
.LBB3_17:
	ldr	r4, .LCPI3_5_tail
	ldr	r7, [sp, #16]                   @ 4-byte Reload
	ldrb	r4, [r4, r7]
	subs	r6, #111
	muls	r6, r4, r6
	lsrs	r4, r6, #13
	bne	.LBB3_22
@ %bb.18:                               @   in Loop: Header=BB3_7 Depth=2
.LBB3_18:
	lsrs	r4, r6, #8
	ldr	r6, .LCPI3_4_tail
	ldrb	r6, [r6, r4]
	movs	r4, #255
	eors	r4, r6
	b	.LBB3_20
.LBB3_19:                               @   in Loop: Header=BB3_7 Depth=2
	movs	r4, #72
	muls	r4, r6, r4
	lsrs	r4, r4, #8
	ldr	r6, .LCPI3_4_tail
	ldrb	r4, [r6, r4]
.LBB3_20:                               @   in Loop: Header=BB3_7 Depth=2
	adds	r0, r2, r0
	ldr	r6, [sp, #24]                   @ 4-byte Reload
	muls	r4, r6, r4
	adds	r4, #255
	lsrs	r4, r4, #8
	uxtb	r4, r4
	movs	r6, #0
.LBB3_21:                               @   Parent Loop BB3_3 Depth=1
                                        @     Parent Loop BB3_7 Depth=2
                                        @ =>    This Inner Loop Header: Depth=3
	ldrb	r7, [r5, r6]
	muls	r7, r4, r7
	adds	r7, #255
	lsrs	r7, r7, #8
	strb	r7, [r0]
	adds	r0, #108
	adds	r6, r6, #1
	cmp	r6, #3
	bne	.LBB3_21
.LBB3_22:                               @   in Loop: Header=BB3_7 Depth=2
	adds	r2, #8
	adds	r3, r3, #1
	cmp	r3, #6
	beq	.Lbridge_tail1
	bl	.LBB3_7
.Lbridge_tail1:
.LBB3_23:                               @   in Loop: Header=BB3_3 Depth=1
	ldr	r0, [sp, #12]                   @ 4-byte Reload
	adds	r0, r0, #1
	cmp	r0, #5
	beq	.Lbridge_tail2
	bl	.LBB3_3
.Lbridge_tail2:
@ %bb.24:
	add	sp, #36
	pop	{r4, r5, r6, r7, pc}

	.p2align 2
.LCPI3_3_tail:
	.long 20016
.LCPI3_4_tail:
	.long curve
.LCPI3_5_tail:
	.long inverse


	.type	conflicts,%object               @ @conflicts
	.section	.rodata.conflicts,"a",%progbits
	.p2align	1, 0x0
conflicts:
	.short	8195                            @ 0x2003
	.short	8199                            @ 0x2007
	.short	8206                            @ 0x200e
	.short	8220                            @ 0x201c
	.short	56                              @ 0x38
	.short	112                             @ 0x70
	.short	224                             @ 0xe0
	.short	448                             @ 0x1c0
	.short	896                             @ 0x380
	.short	1792                            @ 0x700
	.short	7680                            @ 0x1e00
	.short	7168                            @ 0x1c00
	.short	7168                            @ 0x1c00
	.short	8207                            @ 0x200f
	.size	conflicts, 28

	.type	routes,%object                  @ @routes
	.section	.rodata.routes,"a",%progbits
routes:
	.ascii	"\210\210\037"
	.zero	3,17
	.ascii	"\021\021!"
	.ascii	"3\"\""
	.ascii	"#\"2"
	.ascii	"D33"
	.ascii	"DCD"
	.ascii	"UDD"
	.zero	3,85
	.ascii	"fUU"
	.zero	3,102
	.ascii	"wfg"
	.ascii	"wff"
	.ascii	"\210\231/"
	.size	routes, 42

	.type	inverse,%object                 @ @inverse
	.section	.rodata.inverse,"a",%progbits
inverse:
	.ascii	"PLHDA>"
	.size	inverse, 6

	.type	curve,%object                   @ @curve
	.section	.rodata.cst32,"aM",%progbits,32
curve:
	.ascii	"\000\000\001\002\004\b\f\022\032#.:FTbq\200\216\235\253\271\305\321\334\345\355\363\367\373\375\376\377"
	.size	curve, 32

	.ident	"clang version 18.1.6 (https://github.com/ziglang/zig-bootstrap 98bc6bf4fc4009888d33941daf6b600d20a42a56)"
	.section	".note.GNU-stack","",%progbits
	.addrsig
	.addrsig_sym rain_init
	.eabi_attribute	30, 3	@ Tag_ABI_optimization_goals
