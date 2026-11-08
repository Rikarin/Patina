from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_setting_condition import GraphSettingCondition





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
            label (None | str | Unset): What a person calls it, apart from Name, which expressions write. Null or empty for
                the name.
            widget (None | str | Unset): The control an inspector draws: `Default`, `Slider` (a number with a range),
                `Angle` (a number
                in degrees) or `Toggle` (a flag). Null for `Default`.
            visible_if (GraphSettingCondition | None | Unset):
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
    label: None | str | Unset = UNSET
    widget: None | str | Unset = UNSET
    visible_if: GraphSettingCondition | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_setting_condition import GraphSettingCondition # noqa: PLC0415
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

        label: None | str | Unset
        if isinstance(self.label, Unset):
            label = UNSET
        else:
            label = self.label

        widget: None | str | Unset
        if isinstance(self.widget, Unset):
            widget = UNSET
        else:
            widget = self.widget

        visible_if: dict[str, Any] | None | Unset
        if isinstance(self.visible_if, Unset):
            visible_if = UNSET
        elif isinstance(self.visible_if, GraphSettingCondition):
            visible_if = self.visible_if.to_dict()
        else:
            visible_if = self.visible_if


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
        if label is not UNSET:
            field_dict["label"] = label
        if widget is not UNSET:
            field_dict["widget"] = widget
        if visible_if is not UNSET:
            field_dict["visibleIf"] = visible_if

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_setting_condition import GraphSettingCondition # noqa: PLC0415
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


        def _parse_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        label = _parse_label(d.pop("label", UNSET))


        def _parse_widget(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        widget = _parse_widget(d.pop("widget", UNSET))


        def _parse_visible_if(data: object) -> GraphSettingCondition | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                visible_if_type_1 = GraphSettingCondition.from_dict(data)



                return visible_if_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphSettingCondition | None | Unset, data)

        visible_if = _parse_visible_if(d.pop("visibleIf", UNSET))


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
            label=label,
            widget=widget,
            visible_if=visible_if,
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
