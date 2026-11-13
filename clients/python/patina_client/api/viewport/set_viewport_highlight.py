from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.viewport_highlight import ViewportHighlight
from ...models.viewport_state import ViewportState
from typing import cast



def _get_kwargs(
    *,
    body: ViewportHighlight,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/viewport/highlight",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ViewportState | None:
    if response.status_code == 200:
        response_200 = ViewportState.from_dict(response.json())



        return response_200

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ViewportState]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportHighlight,

) -> Response[ViewportState]:
    """ Marks some named meshes and materials (texture sets) on the model in one colour with their outline,
    for every client. Empty lists mark nothing.

    Args:
        body (ViewportHighlight): What the viewport marks on the model (#136): named meshes and
            materials, a texture set being one, washed in one
            colour and outlined over the model as it is shaded, such as the row a client points at in
            its list of texture
            sets. A part an isolation hides is not marked. Both empty marks nothing.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportHighlight,

) -> ViewportState | None:
    """ Marks some named meshes and materials (texture sets) on the model in one colour with their outline,
    for every client. Empty lists mark nothing.

    Args:
        body (ViewportHighlight): What the viewport marks on the model (#136): named meshes and
            materials, a texture set being one, washed in one
            colour and outlined over the model as it is shaded, such as the row a client points at in
            its list of texture
            sets. A part an isolation hides is not marked. Both empty marks nothing.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ViewportState
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportHighlight,

) -> Response[ViewportState]:
    """ Marks some named meshes and materials (texture sets) on the model in one colour with their outline,
    for every client. Empty lists mark nothing.

    Args:
        body (ViewportHighlight): What the viewport marks on the model (#136): named meshes and
            materials, a texture set being one, washed in one
            colour and outlined over the model as it is shaded, such as the row a client points at in
            its list of texture
            sets. A part an isolation hides is not marked. Both empty marks nothing.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportHighlight,

) -> ViewportState | None:
    """ Marks some named meshes and materials (texture sets) on the model in one colour with their outline,
    for every client. Empty lists mark nothing.

    Args:
        body (ViewportHighlight): What the viewport marks on the model (#136): named meshes and
            materials, a texture set being one, washed in one
            colour and outlined over the model as it is shaded, such as the row a client points at in
            its list of texture
            sets. A part an isolation hides is not marked. Both empty marks nothing.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ViewportState
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
