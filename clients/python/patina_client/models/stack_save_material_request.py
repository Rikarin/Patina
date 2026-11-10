from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackSaveMaterialRequest")



@_attrs_define
class StackSaveMaterialRequest:
    """ Saves layers or a mask of an open stack into a project, as a layered material its shelf lists (#135): layers
    become a smart material that unfolds into another stack, and a mask becomes a smart mask another layer takes.

        Attributes:
            project (str): The project it goes into, by its path below `projects/`.
            name (str): What it is called on the shelf.
            layers (list[str]): The layers, by id: a folder brings its children. With Mask, exactly one, whose mask is
                saved.
            mask (bool): Whether to save the layer's mask, with its entries and effects, rather than the layers. Default:
                False.
     """

    project: str
    name: str
    layers: list[str]
    mask: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        name = self.name

        layers = self.layers



        mask = self.mask


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "name": name,
            "layers": layers,
            "mask": mask,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        name = d.pop("name")

        layers = cast(list[str], d.pop("layers"))


        mask = d.pop("mask")

        stack_save_material_request = cls(
            project=project,
            name=name,
            layers=layers,
            mask=mask,
        )


        stack_save_material_request.additional_properties = d
        return stack_save_material_request

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
