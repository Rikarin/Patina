from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_brush import StackBrush
  from ..models.stack_term import StackTerm





T = TypeVar("T", bound="StackBrushTerms")



@_attrs_define
class StackBrushTerms:
    """ What a brush's fields take and how a new brush starts, so a client keeps no list or default of its own.

        Attributes:
            default (StackBrush): A brush. Everything but the radius has the value a new brush has.
            falloffs (list[StackTerm]): How an edge can fall away.
            rotations (list[StackTerm]): How stamps can turn.
            size_spaces (list[StackTerm]): What a radius can be measured in.
            scatter_axes (list[StackTerm]): Which way scatter can move a stamp.
            symmetries (list[StackTerm]): The kinds of symmetry.
            axes (list[StackTerm]): The axes a symmetry can use.
            centres (list[StackTerm]): What a symmetry's plane or axis can pass through.
            tools (list[StackTerm]): What a preset can hold the pointer as.
            fills (list[StackTerm]): How much of a mesh one click of a polygon fill can take.
            maximum_radius (float): The largest radius a stroke takes, in its own units.
            maximum_aspect (float): The longest a stamp can be, against its width.
            maximum_count (int): The most strokes a radial symmetry makes.
     """

    default: StackBrush
    falloffs: list[StackTerm]
    rotations: list[StackTerm]
    size_spaces: list[StackTerm]
    scatter_axes: list[StackTerm]
    symmetries: list[StackTerm]
    axes: list[StackTerm]
    centres: list[StackTerm]
    tools: list[StackTerm]
    fills: list[StackTerm]
    maximum_radius: float
    maximum_aspect: float
    maximum_count: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        default = self.default.to_dict()

        falloffs = []
        for falloffs_item_data in self.falloffs:
            falloffs_item = falloffs_item_data.to_dict()
            falloffs.append(falloffs_item)



        rotations = []
        for rotations_item_data in self.rotations:
            rotations_item = rotations_item_data.to_dict()
            rotations.append(rotations_item)



        size_spaces = []
        for size_spaces_item_data in self.size_spaces:
            size_spaces_item = size_spaces_item_data.to_dict()
            size_spaces.append(size_spaces_item)



        scatter_axes = []
        for scatter_axes_item_data in self.scatter_axes:
            scatter_axes_item = scatter_axes_item_data.to_dict()
            scatter_axes.append(scatter_axes_item)



        symmetries = []
        for symmetries_item_data in self.symmetries:
            symmetries_item = symmetries_item_data.to_dict()
            symmetries.append(symmetries_item)



        axes = []
        for axes_item_data in self.axes:
            axes_item = axes_item_data.to_dict()
            axes.append(axes_item)



        centres = []
        for centres_item_data in self.centres:
            centres_item = centres_item_data.to_dict()
            centres.append(centres_item)



        tools = []
        for tools_item_data in self.tools:
            tools_item = tools_item_data.to_dict()
            tools.append(tools_item)



        fills = []
        for fills_item_data in self.fills:
            fills_item = fills_item_data.to_dict()
            fills.append(fills_item)



        maximum_radius = self.maximum_radius

        maximum_aspect = self.maximum_aspect

        maximum_count = self.maximum_count


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "default": default,
            "falloffs": falloffs,
            "rotations": rotations,
            "sizeSpaces": size_spaces,
            "scatterAxes": scatter_axes,
            "symmetries": symmetries,
            "axes": axes,
            "centres": centres,
            "tools": tools,
            "fills": fills,
            "maximumRadius": maximum_radius,
            "maximumAspect": maximum_aspect,
            "maximumCount": maximum_count,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        d = dict(src_dict)
        default = StackBrush.from_dict(d.pop("default"))




        falloffs = []
        _falloffs = d.pop("falloffs")
        for falloffs_item_data in (_falloffs):
            falloffs_item = StackTerm.from_dict(falloffs_item_data)



            falloffs.append(falloffs_item)


        rotations = []
        _rotations = d.pop("rotations")
        for rotations_item_data in (_rotations):
            rotations_item = StackTerm.from_dict(rotations_item_data)



            rotations.append(rotations_item)


        size_spaces = []
        _size_spaces = d.pop("sizeSpaces")
        for size_spaces_item_data in (_size_spaces):
            size_spaces_item = StackTerm.from_dict(size_spaces_item_data)



            size_spaces.append(size_spaces_item)


        scatter_axes = []
        _scatter_axes = d.pop("scatterAxes")
        for scatter_axes_item_data in (_scatter_axes):
            scatter_axes_item = StackTerm.from_dict(scatter_axes_item_data)



            scatter_axes.append(scatter_axes_item)


        symmetries = []
        _symmetries = d.pop("symmetries")
        for symmetries_item_data in (_symmetries):
            symmetries_item = StackTerm.from_dict(symmetries_item_data)



            symmetries.append(symmetries_item)


        axes = []
        _axes = d.pop("axes")
        for axes_item_data in (_axes):
            axes_item = StackTerm.from_dict(axes_item_data)



            axes.append(axes_item)


        centres = []
        _centres = d.pop("centres")
        for centres_item_data in (_centres):
            centres_item = StackTerm.from_dict(centres_item_data)



            centres.append(centres_item)


        tools = []
        _tools = d.pop("tools")
        for tools_item_data in (_tools):
            tools_item = StackTerm.from_dict(tools_item_data)



            tools.append(tools_item)


        fills = []
        _fills = d.pop("fills")
        for fills_item_data in (_fills):
            fills_item = StackTerm.from_dict(fills_item_data)



            fills.append(fills_item)


        maximum_radius = d.pop("maximumRadius")

        maximum_aspect = d.pop("maximumAspect")

        maximum_count = d.pop("maximumCount")

        stack_brush_terms = cls(
            default=default,
            falloffs=falloffs,
            rotations=rotations,
            size_spaces=size_spaces,
            scatter_axes=scatter_axes,
            symmetries=symmetries,
            axes=axes,
            centres=centres,
            tools=tools,
            fills=fills,
            maximum_radius=maximum_radius,
            maximum_aspect=maximum_aspect,
            maximum_count=maximum_count,
        )


        stack_brush_terms.additional_properties = d
        return stack_brush_terms

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
