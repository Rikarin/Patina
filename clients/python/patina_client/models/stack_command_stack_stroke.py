from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_stroke_op import StackCommandStackStrokeOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_brush import StackBrush
  from ..models.stack_picture import StackPicture
  from ..models.stack_stamp import StackStamp
  from ..models.stack_stroke_source import StackStrokeSource
  from ..models.stack_symmetry import StackSymmetry
  from ..models.stack_view import StackView





T = TypeVar("T", bound="StackCommandStackStroke")



@_attrs_define
class StackCommandStackStroke:
    """ Part of a stroke: where the brush went, never what it left behind. A drag sends several of these with one
    Stroke and they make one undo step, ended by one with Open false.

        Attributes:
            op (StackCommandStackStrokeOp):
            stroke (str): A name the client chose for this drag.
            layer (str): The paint layer, or for a mask any layer.
            channel (str): Which of its channels. Ignored for a mask.
            brush (StackBrush): A brush. Everything but the radius has the value a new brush has.
            colour (list[float]): Red, green, blue and alpha, 0 to 1, which the first part decides. A mask reads the red.
            stamps (list[StackStamp]): Where the brush went since the last part.
            open_ (bool): Whether more of this drag is coming. Default: False.
            mask (bool): Whether this paints the layer's mask. A flat mask becomes a canvas of what it showed; a mask that
                is a picture
                refuses, since nothing yet keeps that picture under paint. Default: False.
            curve (str): free follows the stamps; line and path run through them as control points when it ends; fill takes
                each stamp
                as a click and fills the piece of the mesh under it, whole, as Fill says. Default: 'free'.
            view (None | StackView | Unset):
            symmetry (None | StackSymmetry | Unset):
            mask_layer (int | None | Unset): With Mask, the entry of the mask's stack it paints, counted from the bottom,
                which must be a
                paint entry. Null paints the mask's base.
            source (None | StackStrokeSource | Unset):
            stencil (None | StackPicture | Unset):
            fill (None | str | Unset): With the fill curve, how much of the mesh a click takes: `triangle`, `polygon` (the
                flat polygon it
                is part of), `part` (the named mesh it belongs to) or `shell` (its UV island). It needs a mesh.
     """

    op: StackCommandStackStrokeOp
    stroke: str
    layer: str
    channel: str
    brush: StackBrush
    colour: list[float]
    stamps: list[StackStamp]
    open_: bool = False
    mask: bool = False
    curve: str = 'free'
    view: None | StackView | Unset = UNSET
    symmetry: None | StackSymmetry | Unset = UNSET
    mask_layer: int | None | Unset = UNSET
    source: None | StackStrokeSource | Unset = UNSET
    stencil: None | StackPicture | Unset = UNSET
    fill: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_picture import StackPicture # noqa: PLC0415
        from ..models.stack_stamp import StackStamp # noqa: PLC0415
        from ..models.stack_stroke_source import StackStrokeSource # noqa: PLC0415
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        from ..models.stack_view import StackView # noqa: PLC0415
        op = self.op.value

        stroke = self.stroke

        layer = self.layer

        channel = self.channel

        brush = self.brush.to_dict()

        colour = self.colour



        stamps = []
        for stamps_item_data in self.stamps:
            stamps_item = stamps_item_data.to_dict()
            stamps.append(stamps_item)



        open_ = self.open_

        mask = self.mask

        curve = self.curve

        view: dict[str, Any] | None | Unset
        if isinstance(self.view, Unset):
            view = UNSET
        elif isinstance(self.view, StackView):
            view = self.view.to_dict()
        else:
            view = self.view

        symmetry: dict[str, Any] | None | Unset
        if isinstance(self.symmetry, Unset):
            symmetry = UNSET
        elif isinstance(self.symmetry, StackSymmetry):
            symmetry = self.symmetry.to_dict()
        else:
            symmetry = self.symmetry

        mask_layer: int | None | Unset
        if isinstance(self.mask_layer, Unset):
            mask_layer = UNSET
        else:
            mask_layer = self.mask_layer

        source: dict[str, Any] | None | Unset
        if isinstance(self.source, Unset):
            source = UNSET
        elif isinstance(self.source, StackStrokeSource):
            source = self.source.to_dict()
        else:
            source = self.source

        stencil: dict[str, Any] | None | Unset
        if isinstance(self.stencil, Unset):
            stencil = UNSET
        elif isinstance(self.stencil, StackPicture):
            stencil = self.stencil.to_dict()
        else:
            stencil = self.stencil

        fill: None | str | Unset
        if isinstance(self.fill, Unset):
            fill = UNSET
        else:
            fill = self.fill


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "stroke": stroke,
            "layer": layer,
            "channel": channel,
            "brush": brush,
            "colour": colour,
            "stamps": stamps,
            "open": open_,
            "mask": mask,
            "curve": curve,
        })
        if view is not UNSET:
            field_dict["view"] = view
        if symmetry is not UNSET:
            field_dict["symmetry"] = symmetry
        if mask_layer is not UNSET:
            field_dict["maskLayer"] = mask_layer
        if source is not UNSET:
            field_dict["source"] = source
        if stencil is not UNSET:
            field_dict["stencil"] = stencil
        if fill is not UNSET:
            field_dict["fill"] = fill

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_picture import StackPicture # noqa: PLC0415
        from ..models.stack_stamp import StackStamp # noqa: PLC0415
        from ..models.stack_stroke_source import StackStrokeSource # noqa: PLC0415
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        from ..models.stack_view import StackView # noqa: PLC0415
        d = dict(src_dict)
        op = StackCommandStackStrokeOp(d.pop("op"))




        stroke = d.pop("stroke")

        layer = d.pop("layer")

        channel = d.pop("channel")

        brush = StackBrush.from_dict(d.pop("brush"))




        colour = cast(list[float], d.pop("colour"))


        stamps = []
        _stamps = d.pop("stamps")
        for stamps_item_data in (_stamps):
            stamps_item = StackStamp.from_dict(stamps_item_data)



            stamps.append(stamps_item)


        open_ = d.pop("open")

        mask = d.pop("mask")

        curve = d.pop("curve")

        def _parse_view(data: object) -> None | StackView | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                view_type_1 = StackView.from_dict(data)



                return view_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackView | Unset, data)

        view = _parse_view(d.pop("view", UNSET))


        def _parse_symmetry(data: object) -> None | StackSymmetry | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                symmetry_type_1 = StackSymmetry.from_dict(data)



                return symmetry_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackSymmetry | Unset, data)

        symmetry = _parse_symmetry(d.pop("symmetry", UNSET))


        def _parse_mask_layer(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        mask_layer = _parse_mask_layer(d.pop("maskLayer", UNSET))


        def _parse_source(data: object) -> None | StackStrokeSource | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                source_type_1 = StackStrokeSource.from_dict(data)



                return source_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackStrokeSource | Unset, data)

        source = _parse_source(d.pop("source", UNSET))


        def _parse_stencil(data: object) -> None | StackPicture | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                stencil_type_1 = StackPicture.from_dict(data)



                return stencil_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackPicture | Unset, data)

        stencil = _parse_stencil(d.pop("stencil", UNSET))


        def _parse_fill(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        fill = _parse_fill(d.pop("fill", UNSET))


        stack_command_stack_stroke = cls(
            op=op,
            stroke=stroke,
            layer=layer,
            channel=channel,
            brush=brush,
            colour=colour,
            stamps=stamps,
            open_=open_,
            mask=mask,
            curve=curve,
            view=view,
            symmetry=symmetry,
            mask_layer=mask_layer,
            source=source,
            stencil=stencil,
            fill=fill,
        )


        stack_command_stack_stroke.additional_properties = d
        return stack_command_stack_stroke

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
