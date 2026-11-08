from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.bake_options import BakeOptions





T = TypeVar("T", bound="ProjectBakeSettings")



@_attrs_define
class ProjectBakeSettings:
    """ What a project keeps about baking, in its own file: the options, which texture sets to bake, and the files
    beyond the project's mesh that a bake reads.

        Attributes:
            options (BakeOptions): How a bake runs, with no meshes in it: what a project keeps between bakes and every
                client sends. Each map's
                own settings are a part of their own.
            sets (list[str]): The texture sets a bake makes maps for, by material; empty is all of them.
            high (list[str]): Detailed meshes after the mesh's own, in order, as paths on the Server's machine.
            cage (str): A cage, as a path on the Server's machine, or empty for none.
     """

    options: BakeOptions
    sets: list[str]
    high: list[str]
    cage: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        options = self.options.to_dict()

        sets = self.sets



        high = self.high



        cage = self.cage


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "options": options,
            "sets": sets,
            "high": high,
            "cage": cage,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        d = dict(src_dict)
        options = BakeOptions.from_dict(d.pop("options"))




        sets = cast(list[str], d.pop("sets"))


        high = cast(list[str], d.pop("high"))


        cage = d.pop("cage")

        project_bake_settings = cls(
            options=options,
            sets=sets,
            high=high,
            cage=cage,
        )


        project_bake_settings.additional_properties = d
        return project_bake_settings

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
