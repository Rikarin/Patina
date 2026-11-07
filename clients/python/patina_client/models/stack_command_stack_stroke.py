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
  from ..models.stack_stamp import StackStamp
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
            curve (str): free follows the stamps; line and path run through them as control points when it ends. Default:
                'free'.
            view (None | StackView | Unset):
            mirror (None | str | Unset): x, y or z mirrors a stroke on the mesh through that plane of the mesh, or null.
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
    mirror: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_stamp import StackStamp # noqa: PLC0415
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

        mirror: None | str | Unset
        if isinstance(self.mirror, Unset):
            mirror = UNSET
        else:
            mirror = self.mirror


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
        if mirror is not UNSET:
            field_dict["mirror"] = mirror

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_stamp import StackStamp # noqa: PLC0415
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


        def _parse_mirror(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mirror = _parse_mirror(d.pop("mirror", UNSET))


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
            mirror=mirror,
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
