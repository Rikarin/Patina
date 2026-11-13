from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackDecal")



@_attrs_define
class StackDecal:
    """ A decal (#133): a picture projected through a box placed on the model, kept in the stack as numbers, so it can
    be moved, scaled and turned again. Every length is in the model's own units, the ones
    `POST /viewport/pick` answers in.

        Attributes:
            centre (list[float]): The box's middle: three numbers.
            facing (list[float]): The way the decal faces, out of the surface it lands on: three numbers, any length.
            up (list[float]): Which way is up on the picture: three numbers, made square to the facing.
            width (float): How wide the picture lands. Above zero.
            height (float): How tall it lands. Above zero.
            depth (float): How deep the box reaches, through and out of the surface. Above zero.
            angle (float): How far a face may turn from the facing and still take the picture, in radians, 0 to π: the
                backface cull. Default: 1.3.
            fade (float): How much of the box's depth, from each face inwards, the picture fades over, 0 to 1. Default:
                0.25.
            tiled (bool): Whether the picture repeats past its edges, or is nothing there. Default: False.
            sampling (str): How the picture is read between its pixels: `Bilinear` or `Point`. Default: 'Bilinear'.
            box (list[float] | None | Unset): The box the mesh's `position` bake is normalised in: the least corner's three
                numbers, then the greatest's.
                It is the model's bounds as `POST /viewport/pick` gives them. Null or empty is the unit cube. A bake that
                says the box its position map spans is read through that box instead, so this one only matters for a bake that
                does not.
            warp (list[float] | None | Unset): A warp (#134): where each point of a 3 by 3 grid over the picture lands on
                the decal's square, row by row from
                the top left, as eighteen numbers 0 to 1 across and down. Null or empty bends nothing.
     """

    centre: list[float]
    facing: list[float]
    up: list[float]
    width: float
    height: float
    depth: float
    angle: float = 1.3
    fade: float = 0.25
    tiled: bool = False
    sampling: str = 'Bilinear'
    box: list[float] | None | Unset = UNSET
    warp: list[float] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        centre = self.centre



        facing = self.facing



        up = self.up



        width = self.width

        height = self.height

        depth = self.depth

        angle = self.angle

        fade = self.fade

        tiled = self.tiled

        sampling = self.sampling

        box: list[float] | None | Unset
        if isinstance(self.box, Unset):
            box = UNSET
        elif isinstance(self.box, list):
            box = self.box


        else:
            box = self.box

        warp: list[float] | None | Unset
        if isinstance(self.warp, Unset):
            warp = UNSET
        elif isinstance(self.warp, list):
            warp = self.warp


        else:
            warp = self.warp


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "centre": centre,
            "facing": facing,
            "up": up,
            "width": width,
            "height": height,
            "depth": depth,
            "angle": angle,
            "fade": fade,
            "tiled": tiled,
            "sampling": sampling,
        })
        if box is not UNSET:
            field_dict["box"] = box
        if warp is not UNSET:
            field_dict["warp"] = warp

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        centre = cast(list[float], d.pop("centre"))


        facing = cast(list[float], d.pop("facing"))


        up = cast(list[float], d.pop("up"))


        width = d.pop("width")

        height = d.pop("height")

        depth = d.pop("depth")

        angle = d.pop("angle")

        fade = d.pop("fade")

        tiled = d.pop("tiled")

        sampling = d.pop("sampling")

        def _parse_box(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                box_type_1 = cast(list[float], data)

                return box_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        box = _parse_box(d.pop("box", UNSET))


        def _parse_warp(data: object) -> list[float] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                warp_type_1 = cast(list[float], data)

                return warp_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[float] | None | Unset, data)

        warp = _parse_warp(d.pop("warp", UNSET))


        stack_decal = cls(
            centre=centre,
            facing=facing,
            up=up,
            width=width,
            height=height,
            depth=depth,
            angle=angle,
            fade=fade,
            tiled=tiled,
            sampling=sampling,
            box=box,
            warp=warp,
        )


        stack_decal.additional_properties = d
        return stack_decal

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
