from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphSettingDefinition")



@_attrs_define
class GraphSettingDefinition:
    """ A value a node type or a graph lets an author set.

        Attributes:
            name (str): What it is called.
            default (str): Its default, as text.
            summary (str): One line about it.
            kind (str): Text, Bool, Int or Float.
            group (str): The heading it sits under.
            accepted (list[str]): The values it may take; empty for any.
            minimum (float | None | Unset): Its lower bound, or null for none.
            maximum (float | None | Unset): Its upper bound, or null for none.
            labels (list[str] | None | Unset): What a person calls each of Accepted, in its order. The Server fills it in; a
                client that
                sends a definition leaves it out.
     """

    name: str
    default: str
    summary: str
    kind: str
    group: str
    accepted: list[str]
    minimum: float | None | Unset = UNSET
    maximum: float | None | Unset = UNSET
    labels: list[str] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        default = self.default

        summary = self.summary

        kind = self.kind

        group = self.group

        accepted = self.accepted



        minimum: float | None | Unset
        if isinstance(self.minimum, Unset):
            minimum = UNSET
        else:
            minimum = self.minimum

        maximum: float | None | Unset
        if isinstance(self.maximum, Unset):
            maximum = UNSET
        else:
            maximum = self.maximum

        labels: list[str] | None | Unset
        if isinstance(self.labels, Unset):
            labels = UNSET
        elif isinstance(self.labels, list):
            labels = self.labels


        else:
            labels = self.labels


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "default": default,
            "summary": summary,
            "kind": kind,
            "group": group,
            "accepted": accepted,
        })
        if minimum is not UNSET:
            field_dict["minimum"] = minimum
        if maximum is not UNSET:
            field_dict["maximum"] = maximum
        if labels is not UNSET:
            field_dict["labels"] = labels

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        default = d.pop("default")

        summary = d.pop("summary")

        kind = d.pop("kind")

        group = d.pop("group")

        accepted = cast(list[str], d.pop("accepted"))


        def _parse_minimum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        minimum = _parse_minimum(d.pop("minimum", UNSET))


        def _parse_maximum(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        maximum = _parse_maximum(d.pop("maximum", UNSET))


        def _parse_labels(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                labels_type_1 = cast(list[str], data)

                return labels_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        labels = _parse_labels(d.pop("labels", UNSET))


        graph_setting_definition = cls(
            name=name,
            default=default,
            summary=summary,
            kind=kind,
            group=group,
            accepted=accepted,
            minimum=minimum,
            maximum=maximum,
            labels=labels,
        )


        graph_setting_definition.additional_properties = d
        return graph_setting_definition

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
